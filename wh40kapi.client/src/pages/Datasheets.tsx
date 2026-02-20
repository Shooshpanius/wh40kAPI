import { useEffect, useState } from 'react';

interface Datasheet {
    id: string;
    name: string;
    factionId: string | null;
    role: string | null;
    legend: string | null;
}

export function Datasheets() {
    const [items, setItems] = useState<Datasheet[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [search, setSearch] = useState('');
    const [faction, setFaction] = useState('');
    const [factions, setFactions] = useState<{ id: string; name: string }[]>([]);

    useEffect(() => {
        fetch('/api/factions')
            .then(r => r.ok ? r.json() : Promise.reject('Failed to load factions'))
            .then(setFactions)
            .catch(() => {});
    }, []);

    useEffect(() => {
        const url = faction ? `/api/datasheets?factionId=${encodeURIComponent(faction)}` : '/api/datasheets';
        setLoading(true);
        fetch(url)
            .then(r => r.ok ? r.json() : Promise.reject('Failed to load'))
            .then(setItems)
            .catch(e => setError(String(e)))
            .finally(() => setLoading(false));
    }, [faction]);

    const filtered = items.filter(d =>
        d.name.toLowerCase().includes(search.toLowerCase())
    );

    return (
        <div style={styles.page}>
            <h2 style={styles.title}>Datasheets</h2>
            <div style={styles.filters}>
                <input
                    style={styles.input}
                    placeholder="Search by name..."
                    value={search}
                    onChange={e => setSearch(e.target.value)}
                />
                <select style={styles.select} value={faction} onChange={e => setFaction(e.target.value)}>
                    <option value="">All Factions</option>
                    {factions.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}
                </select>
            </div>
            {loading && <p style={styles.msg}>Loading...</p>}
            {error && <p style={{ ...styles.msg, color: 'tomato' }}>{error}</p>}
            {!loading && !error && (
                <div>
                    <p style={styles.count}>{filtered.length} datasheets</p>
                    <table style={styles.table}>
                        <thead>
                            <tr>
                                <Th>ID</Th><Th>Name</Th><Th>Faction</Th><Th>Role</Th>
                            </tr>
                        </thead>
                        <tbody>
                            {filtered.map(d => (
                                <tr key={d.id} style={styles.row}>
                                    <Td>{d.id}</Td>
                                    <Td>{d.name}</Td>
                                    <Td>{d.factionId ?? '—'}</Td>
                                    <Td>{d.role ?? '—'}</Td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}
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
    page: { maxWidth: 1200, margin: '32px auto', padding: '0 24px' },
    title: { color: '#e8c170' },
    msg: { color: '#ccc' },
    count: { color: '#aaa', marginBottom: '8px' },
    filters: { display: 'flex', gap: '12px', marginBottom: '16px', flexWrap: 'wrap' },
    input: { padding: '8px 12px', background: '#1a1a2e', border: '1px solid #8b0000', color: '#ccc', borderRadius: 4, minWidth: 200 },
    select: { padding: '8px 12px', background: '#1a1a2e', border: '1px solid #8b0000', color: '#ccc', borderRadius: 4 },
    table: { width: '100%', borderCollapse: 'collapse', background: '#1a1a2e' },
    th: { padding: '10px 14px', background: '#8b0000', color: '#fff', textAlign: 'left', borderBottom: '2px solid #c00' },
    td: { padding: '8px 14px', color: '#ccc', borderBottom: '1px solid #333' },
    row: {},
};
