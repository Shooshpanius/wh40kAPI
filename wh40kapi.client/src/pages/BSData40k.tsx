import { useNavigate } from 'react-router-dom';

export function BSData40k() {
    const navigate = useNavigate();
    return (
        <div style={styles.container}>
            <div style={styles.icon}>🔧</div>
            <h1 style={styles.title}>API BSData 40k</h1>
            <p style={styles.subtitle}>Раздел находится в разработке</p>
            <p style={styles.desc}>
                Здесь появится API на основе данных репозитория BSData для Warhammer 40,000.
            </p>
            <button onClick={() => navigate('/')} style={styles.button}>← Вернуться на главную</button>
        </div>
    );
}

const styles: Record<string, React.CSSProperties> = {
    container: { maxWidth: 600, margin: '80px auto', padding: '0 24px', textAlign: 'center' },
    icon: { fontSize: '4rem', marginBottom: '16px' },
    title: { color: '#e8c170', fontSize: '2rem', marginBottom: '8px' },
    subtitle: { color: '#c8c870', fontSize: '1.1rem', marginBottom: '16px' },
    desc: { color: '#aaa', marginBottom: '32px', lineHeight: 1.6 },
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
};
