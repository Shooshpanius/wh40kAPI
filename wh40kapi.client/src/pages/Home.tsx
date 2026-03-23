import { LAST_PR_NUMBER, LAST_PR_DATE } from '../version';

export function Home() {
    return (
        <div style={styles.container}>
            <h1 style={styles.title}>Warhammer 40,000 API</h1>
            <p style={styles.subtitle}>
                Browse Warhammer 40K 10th Edition datasheets, factions, stratagems, detachments and enhancements.
            </p>
            <div style={styles.cards}>
                <Card title="Factions" desc="All playable factions" href="/factions" />
                <Card title="Datasheets" desc="Unit stats and rules" href="/datasheets" />
                <Card title="Detachments" desc="Army detachment rules" href="/detachments" />
                <Card title="Stratagems" desc="Battle stratagems" href="/stratagems" />
                <Card title="Enhancements" desc="Character enhancements" href="/enhancements" />
                <Card title="API Docs" desc="Swagger / Scalar UI" href="/scalar/wh40k" external />
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
    );
}

function Card({ title, desc, href, external }: { title: string; desc: string; href: string; external?: boolean }) {
    const isApiDocs = title === 'API Docs';
    const cardStyle = isApiDocs ? { ...styles.card, ...styles.apiCard } : styles.card;
    return (
        <a href={href} target={external ? '_blank' : undefined} rel={external ? 'noopener noreferrer' : undefined} style={cardStyle}>
            <h3 style={{ margin: 0, color: '#e8c170', fontWeight: isApiDocs ? 700 : 400 }}>{title}</h3>
            <p style={{ margin: '8px 0 0', color: '#aaa', fontWeight: isApiDocs ? 600 : 400 }}>{desc}</p>
        </a>
    );
}

const styles: Record<string, React.CSSProperties> = {
    container: { maxWidth: 900, margin: '40px auto', padding: '0 24px' },
    title: { color: '#e8c170', fontSize: '2rem', marginBottom: '8px' },
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
    apiCard: {
        border: '2px solid #8b0000',
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
