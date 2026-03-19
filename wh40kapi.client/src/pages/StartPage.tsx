import { useEffect, useState } from 'react';

interface ApiStatus {
    wh40k: string | null;
    bsData: string | null;
    ktBsData: string | null;
}

interface VersionInfo {
    prNumber: number;
    mergedAt: string;
}

function formatImportDate(value: string | null | undefined): string {
    if (!value) return 'нет данных';
    const d = new Date(value);
    if (isNaN(d.getTime())) return value;
    return d.toLocaleString('ru-RU', { dateStyle: 'short', timeStyle: 'short' });
}

function formatVersionDate(value: string): string {
    const d = new Date(value);
    if (isNaN(d.getTime())) return value;
    return d.toLocaleDateString('ru-RU', { dateStyle: 'short' });
}

export function StartPage() {
    const [killTeamAvailable, setKillTeamAvailable] = useState(false);
    const [apiStatus, setApiStatus] = useState<ApiStatus | null>(null);
    const [versionInfo, setVersionInfo] = useState<VersionInfo | null>(null);

    useEffect(() => {
        Promise.allSettled([
            fetch('/api/ktbsdata/catalogues')
                .then(r => r.ok ? r.json() : Promise.reject(new Error(`${r.status} ${r.statusText}`)))
                .then((data: unknown) => setKillTeamAvailable(Array.isArray(data) && data.length > 0))
                .catch((e: unknown) => { console.error('Failed to fetch Kill Team catalogues:', e); setKillTeamAvailable(false); }),

            fetch('/api/status')
                .then(r => r.ok ? r.json() : Promise.reject(new Error(`${r.status} ${r.statusText}`)))
                .then((data: ApiStatus) => setApiStatus(data))
                .catch((e: unknown) => { console.error('Failed to fetch API status:', e); }),

            fetch('/api/version')
                .then(r => r.ok ? r.json() : Promise.reject(new Error(`${r.status} ${r.statusText}`)))
                .then((data: VersionInfo) => setVersionInfo(data))
                .catch((e: unknown) => { console.error('Failed to fetch version info:', e); }),
        ]);
    }, []);

    return (
        <div style={styles.container}>
            <h1 style={styles.title}>⚔️ Warhammer 40,000 API Hub</h1>
            {versionInfo && (
                <p style={styles.version}>Beta version #0.0.{versionInfo.prNumber} от {formatVersionDate(versionInfo.mergedAt)}</p>
            )}
            <p style={styles.subtitle}>Выберите один из доступных API</p>
            <div style={styles.cards}>
                <ApiCard
                    title="API Wahapedia 40k"
                    desc="Фракции, юниты, отряды, стратагемы и улучшения."
                    href="/wahapedia"
                    available
                    importDate={formatImportDate(apiStatus?.wh40k)}
                />
                <ApiCard
                    title="API BSData 40k"
                    desc="Данные Warhammer 40,000 из репозитория BSData."
                    href="/bsdata-40k"
                    available
                    importDate={formatImportDate(apiStatus?.bsData)}
                />
                <ApiCard
                    title="API BSData Kill Team"
                    desc="Данные Kill Team из репозитория BSData."
                    href="/bsdata-killteam"
                    available={killTeamAvailable}
                    importDate={formatImportDate(apiStatus?.ktBsData)}
                />
            </div>

            <div style={styles.footer}>
                <div style={{ color: '#ccc' }}>Created by Alexandr Zaytsev</div>
                <div style={{ marginTop: 8 }}>
                    <a href="https://t.me/Shooshpanius" target="_blank" rel="noopener noreferrer" style={styles.button}>Telegram @Shooshpanius</a>
                    <a href="mailto:admin@in-da-house.ru" style={{ ...styles.button, marginLeft: 8 }}>Email</a>
                    <a href="https://github.com/Shooshpanius" target="_blank" rel="noopener noreferrer" style={{ ...styles.button, marginLeft: 8 }}>GitHub</a>
                </div>
            </div>
        </div>
    );
}

function ApiCard({ title, desc, href, available, importDate }: { title: string; desc: string; href: string; available: boolean; importDate?: string }) {
    const content = (
        <>
            <div style={styles.badge}>
                {available
                    ? <span style={styles.badgeAvailable}>Доступно</span>
                    : <span style={styles.badgeSoon}>Скоро</span>
                }
            </div>
            <h2 style={{ margin: '8px 0 0', color: '#e8c170', fontSize: '1.2rem' }}>{title}</h2>
            <p style={{ margin: '8px 0 0', color: '#aaa', fontSize: '0.95rem' }}>{desc}</p>
            {/* If this is the Wahapedia API, show the attribution on a new line with specified color and bold site name */}
            {title.includes('Wahapedia') && (
                <div style={{ marginTop: 8, color: 'rgb(232, 193, 112)', fontSize: '0.9rem' }}>
                    Данные предоставлены сервисом <a href="https://wahapedia.ru/" target="_blank" rel="noopener noreferrer" style={{ color: 'rgb(232, 193, 112)', textDecoration: 'none', fontWeight: 700 }}>Wahapedia</a>
                </div>
            )}
            {importDate && (
                <div style={{ marginTop: 8, color: '#999', fontSize: '0.8rem' }}>
                    Импортировано: {importDate}
                </div>
            )}
        </>
    );
    if (!available) {
        return <div style={{ ...styles.card, ...styles.cardDisabled }} aria-disabled="true">{content}</div>;
    }
    return <a href={href} style={styles.card}>{content}</a>;
}

const styles: Record<string, React.CSSProperties> = {
    container: { maxWidth: 900, margin: '60px auto', padding: '0 24px', textAlign: 'center' },
    title: { color: '#e8c170', fontSize: '2.2rem', marginBottom: '8px' },
    version: { color: '#e8c170', fontSize: '0.9rem', marginBottom: '8px', marginTop: 0 },
    subtitle: { color: '#ccc', marginBottom: '40px', fontSize: '1.1rem' },
    cards: { display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))', gap: '20px', textAlign: 'center' },
    card: {
        background: '#1a1a2e',
        border: '1px solid #8b0000',
        borderRadius: 10,
        padding: '24px',
        textDecoration: 'none',
        display: 'block',
        transition: 'border-color 0.2s',
        textAlign: 'center',
    },
    cardDisabled: {
        opacity: 0.6,
    },
    badge: { marginBottom: 4 },
    badgeAvailable: {
        background: '#2a5e3a',
        color: '#7ddc9a',
        borderRadius: 4,
        padding: '2px 8px',
        fontSize: '0.75rem',
        fontWeight: 700,
    },
    badgeSoon: {
        background: '#3a3a1a',
        color: '#c8c870',
        borderRadius: 4,
        padding: '2px 8px',
        fontSize: '0.75rem',
        fontWeight: 700,
    },
    footer: { marginTop: 48, color: '#999', fontSize: '0.9rem', textAlign: 'center' },
    button: {
        display: 'inline-block',
        padding: '6px 10px',
        background: '#e8c170',
        color: '#0b0b1a',
        borderRadius: 6,
        textDecoration: 'none',
        fontSize: '0.85rem',
    },
};
