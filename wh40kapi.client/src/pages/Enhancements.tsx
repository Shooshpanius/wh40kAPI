import { useEffect, useState } from 'react';

interface Item {
    id: string;
    name: string;
    factionId: string | null;
    cost: string | null;
    detachment: string | null;
}

export function Enhancements() {
    const [items, setItems] = useState<Item[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [search, setSearch] = useState('');

    useEffect(() => {
        fetch('/api/enhancements')
            .then(r => r.ok ? r.json() : Promise.reject('Failed to load'))
            .then(setItems)
            .catch(e => setError(String(e)))
            .finally(() => setLoading(false));
    }, []);

    const filtered = items.filter(d => d.name.toLowerCase().includes(search.toLowerCase()));

    if (loading) return <div style={styles.page}><p style={styles.msg}>Loading...</p></div>;
    if (error) return <div style={styles.page}><p style={{ ...styles.msg, color: 'tomato' }}>{error}</p></div>;

    return (
        <div style={styles.page}>
            <h2 style={styles.title}>Enhancements ({filtered.length})</h2>
            <input style={styles.input} placeholder="Search..." value={search} onChange={e => setSearch(e.target.value)} />
            <table style={styles.table}>
                <thead><tr><Th>Name</Th><Th>Faction</Th><Th>Cost</Th><Th>Detachment</Th></tr></thead>
                <tbody>
                    {filtered.map(e => (
                        <tr key={e.id}>
                            <Td>{e.name}</Td><Td>{e.factionId ?? '—'}</Td>
                            <Td>{e.cost ?? '—'}</Td><Td>{e.detachment ?? '—'}</Td>
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
    page: { maxWidth: 1200, margin: '32px auto', padding: '0 24px' },
    title: { color: '#e8c170' },
    msg: { color: '#ccc' },
    input: { padding: '8px 12px', background: '#1a1a2e', border: '1px solid #8b0000', color: '#ccc', borderRadius: 4, marginBottom: 16, minWidth: 250 },
    table: { width: '100%', borderCollapse: 'collapse', background: '#1a1a2e' },
    th: { padding: '10px 14px', background: '#8b0000', color: '#fff', textAlign: 'left', borderBottom: '2px solid #c00' },
    td: { padding: '8px 14px', color: '#ccc', borderBottom: '1px solid #333' },
};
