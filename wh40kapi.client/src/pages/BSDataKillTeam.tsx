export function BSDataKillTeam() {
    return (
        <div style={styles.pageWrapper}>
            <div style={styles.tabBar}>
                <h2 style={styles.tabTitle}>API BSData Kill Team</h2>
            </div>
            <iframe src="/scalar/ktbsdata" style={styles.iframe} title="BSData Kill Team API Docs" />
        </div>
    );
}

const styles: Record<string, React.CSSProperties> = {
    pageWrapper: { display: 'flex', flexDirection: 'column', height: 'calc(100vh - 56px)' },
    tabBar: { display: 'flex', alignItems: 'center', padding: '12px 24px', borderBottom: '1px solid #333' },
    tabTitle: { color: '#e8c170', fontSize: '1.4rem', margin: 0 },
    iframe: { flex: 1, border: 'none', width: '100%' },
};


