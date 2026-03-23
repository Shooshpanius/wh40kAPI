export function Home() {
    return (
        <div style={styles.page}>
            <div style={styles.header}>
                <h1 style={styles.title}>Warhammer 40,000 API</h1>
            </div>

            <iframe
                src="/scalar/wh40k"
                style={styles.iframe}
                title="Wahapedia 40k API Docs"
            />
        </div>
    );
}

const styles: Record<string, React.CSSProperties> = {
    page: { display: 'flex', flexDirection: 'column', height: 'calc(100vh - 56px)' },
    header: { display: 'flex', alignItems: 'center', padding: '12px 24px', borderBottom: '1px solid #333' },
    title: { color: '#e8c170', fontSize: '1.4rem', margin: 0 },
    iframe: { flex: 1, border: 'none', width: '100%' },
};
