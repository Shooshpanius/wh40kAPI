import { useEffect, useState } from 'react';

interface Item {
    id: string;
    name: string;
    factionId: string | null;
    legend: string | null;
    type: string | null;
}

export function Detachments() {
    const [items, setItems] = useState<Item[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        fetch('/api/detachments')
            .then(r => r.ok ? r.json() : Promise.reject('Failed to load'))
            .then(setItems)
            .catch(e => setError(String(e)))
            .finally(() => setLoading(false));
    }, []);

    if (loading) return <div style={styles.page}><p style={styles.msg}>Loading...</p></div>;
    if (error) return <div style={styles.page}><p style={{ ...styles.msg, color: 'tomato' }}>{error}</p></div>;

    return (
        <div style={styles.page}>
            <h2 style={styles.title}>Detachments ({items.length})</h2>
            <table style={styles.table}>
                <thead><tr><Th>ID</Th><Th>Name</Th><Th>Faction</Th><Th>Type</Th></tr></thead>
                <tbody>
                    {items.map(d => (
                        <tr key={d.id}>
                            <Td>{d.id}</Td><Td>{d.name}</Td><Td>{d.factionId ?? '—'}</Td><Td>{d.type ?? '—'}</Td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}

function Th({ children }: { children: React.ReactNode }) { return <th style={styles.th}>{children}</th>; }
function Td({ children }: { children: React.ReactNode }) { return <td style={styles.td}>{children}</td>; }

const styles: Record<string, React.CSSProperties> = {
    page: { maxWidth: 1000, margin: '32px auto', padding: '0 24px' },
    title: { color: '#e8c170' },
    msg: { color: '#ccc' },
    table: { width: '100%', borderCollapse: 'collapse', background: '#1a1a2e' },
    th: { padding: '10px 14px', background: '#8b0000', color: '#fff', textAlign: 'left', borderBottom: '2px solid #c00' },
    td: { padding: '8px 14px', color: '#ccc', borderBottom: '1px solid #333' },
};
