import { useEffect, useState } from 'react';

interface Faction {
    id: string;
    name: string;
    link: string | null;
}

export function Factions() {
    const [factions, setFactions] = useState<Faction[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        fetch('/api/wh40k/factions')
            .then(r => r.ok ? r.json() : Promise.reject('Failed to load'))
            .then(setFactions)
            .catch(e => setError(String(e)))
            .finally(() => setLoading(false));
    }, []);

    if (loading) return <div style={styles.page}><p style={styles.msg}>Loading...</p></div>;
    if (error) return <div style={styles.page}><p style={{ ...styles.msg, color: 'tomato' }}>{error}</p></div>;

    return (
        <div style={styles.page}>
            <h2 style={styles.title}>Factions ({factions.length})</h2>
            <table style={styles.table}>
                <thead>
                    <tr>
                        <Th>ID</Th><Th>Name</Th><Th>Link</Th>
                    </tr>
                </thead>
                <tbody>
                    {factions.map(f => (
                        <tr key={f.id} style={styles.row}>
                            <Td>{f.id}</Td>
                            <Td>{f.name}</Td>
                            <Td>{f.link ? <a href={f.link} target="_blank" rel="noopener noreferrer" style={styles.link}>{f.link}</a> : '—'}</Td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}

function Th({ children }: { children: React.ReactNode }) {
    return <th style={styles.th}>{children}</th>;
}
function Td({ children }: { children: React.ReactNode }) {
    return <td style={styles.td}>{children}</td>;
}

const styles: Record<string, React.CSSProperties> = {
    page: { maxWidth: 1000, margin: '32px auto', padding: '0 24px' },
    title: { color: '#e8c170' },
    msg: { color: '#ccc' },
    table: { width: '100%', borderCollapse: 'collapse', background: '#1a1a2e' },
    th: { padding: '10px 14px', background: '#8b0000', color: '#fff', textAlign: 'left', borderBottom: '2px solid #c00' },
    td: { padding: '8px 14px', color: '#ccc', borderBottom: '1px solid #333' },
    row: {},
    link: { color: '#e8c170' },
};
