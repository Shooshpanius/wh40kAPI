import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';

interface Catalogue {
    id: string;
    name: string;
    revision: number;
    fetchedAt: string | null;
}

interface Unit {
    id: string;
    name: string;
    entryType: string | null;
    points: string | null;
}

export function BSDataKillTeam() {
    const navigate = useNavigate();
    const [catalogues, setCatalogues] = useState<Catalogue[]>([]);
    const [selectedCatalogue, setSelectedCatalogue] = useState<Catalogue | null>(null);
    const [units, setUnits] = useState<Unit[]>([]);
    const [loading, setLoading] = useState(true);
    const [unitsLoading, setUnitsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        fetch('/api/ktbsdata-catalogues')
            .then(r => r.ok ? r.json() : r.text().then(t => Promise.reject(`${r.status} ${r.statusText}: ${t}`)))
            .then(setCatalogues)
            .catch(e => setError('Failed to load catalogues: ' + String(e)))
            .finally(() => setLoading(false));
    }, []);

    const loadUnits = (catalogue: Catalogue) => {
        setSelectedCatalogue(catalogue);
        setUnitsLoading(true);
        fetch(`/api/ktbsdata-catalogues/${encodeURIComponent(catalogue.id)}/units`)
            .then(r => r.ok ? r.json() : r.text().then(t => Promise.reject(`${r.status} ${r.statusText}: ${t}`)))
            .then(setUnits)
            .catch(() => setUnits([]))
            .finally(() => setUnitsLoading(false));
    };

    if (loading) {
        return (
            <div style={styles.page}>
                <p style={styles.msg}>Loading...</p>
            </div>
        );
    }

    if (error || catalogues.length === 0) {
        return (
            <div style={styles.container}>
                <div style={styles.icon}>🎯</div>
                <h1 style={styles.title}>API BSData Kill Team</h1>
                {error
                    ? <p style={{ color: 'tomato' }}>{error}</p>
                    : <p style={styles.subtitle}>Данные ещё не загружены. Используйте кнопку в разделе Admin.</p>
                }
                <p style={styles.desc}>
                    Данные Kill Team из репозитория{' '}
                    <a href="https://github.com/BSData/wh40k-killteam" target="_blank" rel="noopener noreferrer" style={styles.link}>
                        BSData/wh40k-killteam
                    </a>.
                </p>
                <div style={{ display: 'flex', gap: 12, justifyContent: 'center', flexWrap: 'wrap' }}>
                    <button onClick={() => navigate('/')} style={styles.button}>← Вернуться на главную</button>
                    <a href="/scalar/v1" target="_blank" rel="noopener noreferrer" style={styles.buttonLink}>📖 API Docs</a>
                </div>
            </div>
        );
    }

    return (
        <div style={styles.page}>
            <div style={styles.header}>
                <h2 style={styles.title}>API BSData Kill Team</h2>
                <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
                    <a href="/scalar/v1" target="_blank" rel="noopener noreferrer" style={styles.buttonLink}>📖 API Docs</a>
                    <button onClick={() => navigate('/')} style={styles.btnSecondary}>← Главная</button>
                </div>
            </div>
            <p style={styles.hint}>
                Данные из репозитория{' '}
                <a href="https://github.com/BSData/wh40k-killteam" target="_blank" rel="noopener noreferrer" style={styles.link}>
                    BSData/wh40k-killteam
                </a>. Каталогов: {catalogues.length}.
            </p>

            <div style={styles.layout}>
                <div style={styles.catalogueList}>
                    <h3 style={styles.sectionTitle}>Каталоги (Фракции)</h3>
                    {catalogues.map(c => (
                        <div
                            key={c.id}
                            style={{
                                ...styles.catalogueItem,
                                ...(selectedCatalogue?.id === c.id ? styles.catalogueItemActive : {}),
                            }}
                            onClick={() => loadUnits(c)}
                        >
                            <div style={{ fontWeight: 600, color: '#e8c170' }}>{c.name}</div>
                            <div style={{ fontSize: '0.75rem', color: '#888', marginTop: 2 }}>
                                Rev. {c.revision}
                                {c.fetchedAt && ` · ${new Date(c.fetchedAt).toLocaleDateString()}`}
                            </div>
                        </div>
                    ))}
                </div>

                <div style={styles.unitPanel}>
                    {!selectedCatalogue && (
                        <p style={styles.hint}>← Выберите каталог для просмотра юнитов</p>
                    )}
                    {selectedCatalogue && (
                        <>
                            <h3 style={styles.sectionTitle}>{selectedCatalogue.name}</h3>
                            {unitsLoading && <p style={styles.hint}>Loading...</p>}
                            {!unitsLoading && units.length === 0 && (
                                <p style={styles.hint}>Юниты не найдены.</p>
                            )}
                            {!unitsLoading && units.length > 0 && (
                                <table style={styles.table}>
                                    <thead>
                                        <tr>
                                            <Th>Name</Th>
                                            <Th>Type</Th>
                                            <Th>Pts</Th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {units.map(u => (
                                            <tr key={u.id} style={styles.row}>
                                                <Td>{u.name}</Td>
                                                <Td>{u.entryType ?? '—'}</Td>
                                                <Td>{u.points ?? '—'}</Td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            )}
                        </>
                    )}
                </div>
            </div>
        </div>
    );
}

function Th({ children }: { children: React.ReactNode }) {
    return <th style={thStyle}>{children}</th>;
}
function Td({ children }: { children: React.ReactNode }) {
    return <td style={tdStyle}>{children}</td>;
}

const thStyle: React.CSSProperties = { padding: '8px 12px', background: '#8b0000', color: '#fff', textAlign: 'left', borderBottom: '2px solid #c00' };
const tdStyle: React.CSSProperties = { padding: '6px 12px', color: '#ccc', borderBottom: '1px solid #333' };

const styles: Record<string, React.CSSProperties> = {
    page: { maxWidth: 1100, margin: '32px auto', padding: '0 24px' },
    container: { maxWidth: 600, margin: '80px auto', padding: '0 24px', textAlign: 'center' },
    header: { display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12, marginBottom: 12 },
    icon: { fontSize: '4rem', marginBottom: '16px' },
    title: { color: '#e8c170', fontSize: '1.6rem', margin: 0 },
    subtitle: { color: '#c8c870', fontSize: '1.1rem', marginBottom: '16px' },
    desc: { color: '#aaa', marginBottom: '32px', lineHeight: 1.6 },
    hint: { color: '#aaa', marginBottom: 12 },
    msg: { color: '#ccc' },
    link: { color: '#e8c170' },
    sectionTitle: { color: '#e8c170', marginBottom: 12 },
    button: {
        padding: '10px 20px',
        background: '#e8c170',
        color: '#0b0b1a',
        border: 'none',
        borderRadius: 6,
        fontSize: '0.95rem',
        cursor: 'pointer',
        fontWeight: 600,
    },
    buttonLink: {
        display: 'inline-block',
        padding: '8px 16px',
        background: '#8b0000',
        color: '#fff',
        border: 'none',
        borderRadius: 6,
        fontSize: '0.9rem',
        cursor: 'pointer',
        fontWeight: 600,
        textDecoration: 'none',
    },
    btnSecondary: {
        padding: '8px 16px',
        background: 'transparent',
        color: '#aaa',
        border: '1px solid #555',
        borderRadius: 6,
        fontSize: '0.9rem',
        cursor: 'pointer',
    },
    layout: { display: 'flex', gap: 24, alignItems: 'flex-start', flexWrap: 'wrap' },
    catalogueList: { width: 260, flexShrink: 0, maxHeight: '70vh', overflowY: 'auto' },
    catalogueItem: {
        background: '#1a1a2e',
        border: '1px solid #333',
        borderRadius: 6,
        padding: '10px 14px',
        marginBottom: 8,
        cursor: 'pointer',
    },
    catalogueItemActive: { border: '1px solid #8b0000' },
    unitPanel: { flex: 1, minWidth: 0 },
    table: { width: '100%', borderCollapse: 'collapse', background: '#1a1a2e' },
    row: {},
};
