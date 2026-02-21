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

interface Profile {
    id: string;
    unitId: string;
    name: string;
    typeName: string;
    characteristics: string | null;
}

export function BSDataKillTeam() {
    const navigate = useNavigate();
    const [catalogues, setCatalogues] = useState<Catalogue[]>([]);
    const [selectedCatalogue, setSelectedCatalogue] = useState<Catalogue | null>(null);
    const [units, setUnits] = useState<Unit[]>([]);
    const [selectedUnit, setSelectedUnit] = useState<Unit | null>(null);
    const [profiles, setProfiles] = useState<Profile[]>([]);
    const [loading, setLoading] = useState(true);
    const [unitsLoading, setUnitsLoading] = useState(false);
    const [profilesLoading, setProfilesLoading] = useState(false);
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
        setSelectedUnit(null);
        setProfiles([]);
        setUnitsLoading(true);
        fetch(`/api/ktbsdata-catalogues/${encodeURIComponent(catalogue.id)}/units`)
            .then(r => r.ok ? r.json() : r.text().then(t => Promise.reject(`${r.status} ${r.statusText}: ${t}`)))
            .then(setUnits)
            .catch(() => setUnits([]))
            .finally(() => setUnitsLoading(false));
    };

    const loadProfiles = (unit: Unit) => {
        setSelectedUnit(unit);
        setProfilesLoading(true);
        fetch(`/api/ktbsdata-units/${encodeURIComponent(unit.id)}/profiles`)
            .then(r => r.ok ? r.json() : r.text().then(t => Promise.reject(`${r.status} ${r.statusText}: ${t}`)))
            .then(setProfiles)
            .catch(() => setProfiles([]))
            .finally(() => setProfilesLoading(false));
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

    const operativeProfiles = profiles.filter(p => p.typeName === 'Operative');
    const abilityProfiles = profiles.filter(p => p.typeName === 'Abilities');
    const otherProfiles = profiles.filter(p => p.typeName !== 'Operative' && p.typeName !== 'Abilities');

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
                {/* Left panel: catalogues */}
                <div style={styles.catalogueList}>
                    <h3 style={styles.sectionTitle}>Каталоги (Kill Teams)</h3>
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

                {/* Middle panel: units list */}
                <div style={styles.unitPanel}>
                    {!selectedCatalogue && (
                        <p style={styles.hint}>← Выберите каталог для просмотра оперативников</p>
                    )}
                    {selectedCatalogue && (
                        <>
                            <h3 style={styles.sectionTitle}>{selectedCatalogue.name}</h3>
                            {unitsLoading && <p style={styles.hint}>Loading...</p>}
                            {!unitsLoading && units.length === 0 && (
                                <p style={styles.hint}>Оперативники не найдены.</p>
                            )}
                            {!unitsLoading && units.length > 0 && (
                                <table style={styles.table}>
                                    <thead>
                                        <tr>
                                            <Th>Оперативник</Th>
                                            <Th>Тип</Th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {units.map(u => (
                                            <tr
                                                key={u.id}
                                                style={{
                                                    ...styles.row,
                                                    ...(selectedUnit?.id === u.id ? styles.rowActive : {}),
                                                    cursor: 'pointer',
                                                }}
                                                onClick={() => loadProfiles(u)}
                                            >
                                                <Td>{u.name}</Td>
                                                <Td>{u.entryType ?? '—'}</Td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            )}
                        </>
                    )}
                </div>

                {/* Right panel: operative profile details */}
                {selectedUnit && (
                    <div style={styles.profilePanel}>
                        <h3 style={{ ...styles.sectionTitle, borderBottom: '1px solid #333', paddingBottom: 8 }}>
                            {selectedUnit.name}
                        </h3>
                        {profilesLoading && <p style={styles.hint}>Loading...</p>}
                        {!profilesLoading && profiles.length === 0 && (
                            <p style={styles.hint}>Профили не найдены.</p>
                        )}

                        {/* Operative stats */}
                        {operativeProfiles.map(p => {
                            const stats = p.characteristics ? tryParseJson(p.characteristics) : null;
                            return (
                                <div key={p.id} style={styles.statBlock}>
                                    <div style={styles.statBlockTitle}>⚙ Operative</div>
                                    {stats && (
                                        <div style={styles.statsGrid}>
                                            {Object.entries(stats).map(([k, v]) => (
                                                <div key={k} style={styles.statCell}>
                                                    <div style={styles.statLabel}>{k}</div>
                                                    <div style={styles.statValue}>{String(v)}</div>
                                                </div>
                                            ))}
                                        </div>
                                    )}
                                </div>
                            );
                        })}

                        {/* Other profiles grouped by typeName (weapons, actions, etc.) */}
                        {Object.entries(
                            otherProfiles.reduce<Record<string, Profile[]>>((acc, p) => {
                                (acc[p.typeName] ??= []).push(p);
                                return acc;
                            }, {})
                        ).map(([typeName, typeProfiles]) => (
                            <div key={typeName} style={{ marginTop: 12 }}>
                                <div style={styles.profileGroupTitle}>{typeName}</div>
                                <table style={styles.table}>
                                    <thead>
                                        <tr>
                                            <Th>Название</Th>
                                            <Th>Параметры</Th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {typeProfiles.map(p => {
                                            const stats = p.characteristics ? tryParseJson(p.characteristics) : null;
                                            const parts = stats
                                                ? Object.entries(stats).map(([k, v]) => `${k}: ${v}`)
                                                : [];
                                            const summary = parts.length > 0
                                                ? parts.slice(0, 6).join(' | ') + (parts.length > 6 ? ' …' : '')
                                                : '—';
                                            return (
                                                <tr key={p.id} style={styles.row}>
                                                    <Td>{p.name}</Td>
                                                    <Td><span style={{ fontSize: '0.8rem' }}>{summary}</span></Td>
                                                </tr>
                                            );
                                        })}
                                    </tbody>
                                </table>
                            </div>
                        ))}

                        {/* Abilities */}
                        {abilityProfiles.length > 0 && (
                            <div style={{ marginTop: 12 }}>
                                <div style={styles.profileGroupTitle}>Abilities</div>
                                {abilityProfiles.map(p => {
                                    const stats = p.characteristics ? tryParseJson(p.characteristics) : null;
                                    const abilityText = stats ? Object.values(stats).join(' ') : '';
                                    return (
                                        <div key={p.id} style={styles.abilityBlock}>
                                            <div style={styles.abilityName}>{p.name}</div>
                                            {abilityText && <div style={styles.abilityText}>{abilityText}</div>}
                                        </div>
                                    );
                                })}
                            </div>
                        )}
                    </div>
                )}
            </div>
        </div>
    );
}

function tryParseJson(s: string): Record<string, unknown> | null {
    try { return JSON.parse(s); } catch (e) { console.warn('Failed to parse characteristics JSON:', e); return null; }
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
    page: { maxWidth: 1300, margin: '32px auto', padding: '0 24px' },
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
    catalogueList: { width: 220, flexShrink: 0, maxHeight: '80vh', overflowY: 'auto' },
    catalogueItem: {
        background: '#1a1a2e',
        border: '1px solid #333',
        borderRadius: 6,
        padding: '10px 14px',
        marginBottom: 8,
        cursor: 'pointer',
    },
    catalogueItemActive: { border: '1px solid #8b0000' },
    unitPanel: { width: 260, flexShrink: 0 },
    profilePanel: { flex: 1, minWidth: 0, background: '#12122a', border: '1px solid #333', borderRadius: 8, padding: '16px 20px' },
    table: { width: '100%', borderCollapse: 'collapse', background: '#1a1a2e' },
    row: {},
    rowActive: { background: '#2a1a1a' },
    statBlock: { background: '#1a1a2e', border: '1px solid #8b0000', borderRadius: 6, padding: 12, marginBottom: 12 },
    statBlockTitle: { color: '#e8c170', fontWeight: 700, fontSize: '0.85rem', marginBottom: 8, textTransform: 'uppercase', letterSpacing: 1 },
    statsGrid: { display: 'flex', flexWrap: 'wrap', gap: 8 },
    statCell: { background: '#0b0b1a', border: '1px solid #444', borderRadius: 4, padding: '4px 8px', textAlign: 'center', minWidth: 52 },
    statLabel: { color: '#888', fontSize: '0.7rem', textTransform: 'uppercase', marginBottom: 2 },
    statValue: { color: '#e8c170', fontWeight: 700, fontSize: '1rem' },
    profileGroupTitle: { color: '#aaa', fontSize: '0.8rem', textTransform: 'uppercase', letterSpacing: 1, marginBottom: 6 },
    abilityBlock: { background: '#1a1a2e', border: '1px solid #333', borderRadius: 4, padding: '8px 12px', marginBottom: 6 },
    abilityName: { color: '#e8c170', fontWeight: 600, fontSize: '0.9rem', marginBottom: 4 },
    abilityText: { color: '#bbb', fontSize: '0.82rem', lineHeight: 1.5 },
};
