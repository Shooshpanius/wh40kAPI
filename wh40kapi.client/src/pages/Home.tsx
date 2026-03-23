import { useState } from 'react';
import { LAST_PR_NUMBER, LAST_PR_DATE } from '../version';

export function Home() {
    const [view, setView] = useState<'docs' | 'browser'>('docs');

    return (
        <div style={styles.page}>
            <div style={styles.header}>
                <h1 style={styles.title}>Warhammer 40,000 API</h1>
                <div style={styles.tabs}>
                    <button
                        style={{ ...styles.tab, ...(view === 'docs' ? styles.tabActive : {}) }}
                        onClick={() => setView('docs')}
                    >
                        📖 API Docs
                    </button>
                    <button
                        style={{ ...styles.tab, ...(view === 'browser' ? styles.tabActive : {}) }}
                        onClick={() => setView('browser')}
                    >
                        🔍 Браузер данных
                    </button>
                </div>
            </div>

            {view === 'docs' && (
                <iframe
                    src="/scalar/wh40k"
                    style={styles.iframe}
                    title="Wahapedia 40k API Docs"
                />
            )}

            {view === 'browser' && (
                <div style={styles.container}>
                    <p style={styles.subtitle}>
                        Browse Warhammer 40K 10th Edition datasheets, factions, stratagems, detachments and enhancements.
                    </p>
                    <div style={styles.cards}>
                        <Card title="Factions" desc="All playable factions" href="/factions" />
                        <Card title="Datasheets" desc="Unit stats and rules" href="/datasheets" />
                        <Card title="Detachments" desc="Army detachment rules" href="/detachments" />
                        <Card title="Stratagems" desc="Battle stratagems" href="/stratagems" />
                        <Card title="Enhancements" desc="Character enhancements" href="/enhancements" />
                    </div>

                    <div style={styles.footer}>
                        <div style={{ color: 'rgb(232, 193, 112)', fontSize: '0.95rem' }}>
                            Данные предоставлены сервисом <a href="https://wahapedia.ru/" target="_blank" rel="noopener noreferrer" style={{ color: 'rgb(232, 193, 112)', textDecoration: 'none', fontWeight: 700 }}>Wahapedia</a>
                        </div>

                        <div style={{ marginTop: 8, color: '#ccc' }}>Created by Alexandr Zaytsev</div>

                        <div style={{ marginTop: 8 }}>
                            <a href="https://t.me/Shooshpanius" target="_blank" rel="noopener noreferrer" style={styles.button}>Telegram @Shooshpanius</a>
                            <a href="mailto:admin@in-da-house.ru" style={{ ...styles.button, marginLeft: 8 }}>Email</a>
                            <a href="https://github.com/Shooshpanius" target="_blank" rel="noopener noreferrer" style={{ ...styles.button, marginLeft: 8 }}>GitHub</a>
                        </div>

                        <div style={{ marginTop: 12, color: '#888', fontSize: '0.8rem' }}>
                            Beta version #0.0.{LAST_PR_NUMBER} от {LAST_PR_DATE}
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}

function Card({ title, desc, href, external }: { title: string; desc: string; href: string; external?: boolean }) {
    return (
        <a href={href} target={external ? '_blank' : undefined} rel={external ? 'noopener noreferrer' : undefined} style={styles.card}>
            <h3 style={{ margin: 0, color: '#e8c170', fontWeight: 400 }}>{title}</h3>
            <p style={{ margin: '8px 0 0', color: '#aaa', fontWeight: 400 }}>{desc}</p>
        </a>
    );
}

const styles: Record<string, React.CSSProperties> = {
    page: { display: 'flex', flexDirection: 'column', height: 'calc(100vh - 56px)' },
    header: { display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 12, padding: '12px 24px', borderBottom: '1px solid #333' },
    title: { color: '#e8c170', fontSize: '1.4rem', margin: 0 },
    tabs: { display: 'flex', gap: 8 },
    tab: {
        padding: '8px 16px',
        background: 'transparent',
        color: '#aaa',
        border: '1px solid #555',
        borderRadius: 6,
        fontSize: '0.9rem',
        cursor: 'pointer',
        fontWeight: 400,
    },
    tabActive: {
        background: '#8b0000',
        color: '#fff',
        border: '1px solid #8b0000',
        fontWeight: 600,
    },
    iframe: { flex: 1, border: 'none', width: '100%' },
    container: { maxWidth: 900, margin: '40px auto', padding: '0 24px' },
    subtitle: { color: '#ccc', marginBottom: '32px' },
    cards: { display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(220px, 1fr))', gap: '16px' },
    card: {
        background: '#1a1a2e',
        border: '1px solid #8b0000',
        borderRadius: 8,
        padding: '20px',
        textDecoration: 'none',
        display: 'block',
        transition: 'border-color 0.2s',
    },
    footer: { marginTop: 28, color: '#999', fontSize: '0.9rem', textAlign: 'center' },
    link: { color: '#e8c170', textDecoration: 'none' },
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
