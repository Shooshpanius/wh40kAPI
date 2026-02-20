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
                <Card title="API Docs" desc="Swagger / Scalar UI" href="/scalar/v1" external />
            </div>
        </div>
    );
}

function Card({ title, desc, href, external }: { title: string; desc: string; href: string; external?: boolean }) {
    return (
        <a href={href} target={external ? '_blank' : undefined} rel={external ? 'noopener noreferrer' : undefined} style={styles.card}>
            <h3 style={{ margin: 0, color: '#e8c170' }}>{title}</h3>
            <p style={{ margin: '8px 0 0', color: '#aaa' }}>{desc}</p>
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
};
