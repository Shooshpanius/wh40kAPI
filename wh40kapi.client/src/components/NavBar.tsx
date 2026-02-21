import { NavLink } from 'react-router-dom';

export function NavBar() {
    return (
        <nav style={styles.nav}>
            <NavLink to="/" style={styles.brand} end>⚔️ WH40K API</NavLink>
            <div style={styles.links}>
                <NavLink to="/wahapedia" style={navStyle}>Wahapedia</NavLink>
                <NavLink to="/factions" style={navStyle}>Factions</NavLink>
                <NavLink to="/datasheets" style={navStyle}>Datasheets</NavLink>
                <NavLink to="/detachments" style={navStyle}>Detachments</NavLink>
                <NavLink to="/stratagems" style={navStyle}>Stratagems</NavLink>
                <NavLink to="/enhancements" style={navStyle}>Enhancements</NavLink>
                <NavLink to="/admin" style={navStyle}>Admin</NavLink>
            </div>
        </nav>
    );
}

const navStyle = ({ isActive }: { isActive: boolean }) => ({
    color: isActive ? '#e8c170' : '#ccc',
    textDecoration: 'none',
    fontWeight: isActive ? 700 : 400,
    padding: '0 12px',
});

const styles: Record<string, React.CSSProperties> = {
    nav: {
        background: '#1a1a2e',
        padding: '12px 24px',
        display: 'flex',
        alignItems: 'center',
        gap: '16px',
        borderBottom: '2px solid #8b0000',
    },
    brand: {
        color: '#e8c170',
        fontWeight: 700,
        fontSize: '1.2rem',
        marginRight: '24px',
        textDecoration: 'none',
    },
    links: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: '4px',
    },
};
