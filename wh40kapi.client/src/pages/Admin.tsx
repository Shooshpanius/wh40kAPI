import { useState } from 'react';

interface DbStatus {
    lastUpdate: string | null;
    factions: number;
    datasheets: number;
    abilities: number;
    detachments: number;
    stratagems: number;
    enhancements: number;
    sources: number;
}

interface BsDataStatus {
    catalogues: number;
    units: number;
    profiles: number;
}

export function Admin() {
    const [password, setPassword] = useState('');
    const [authenticated, setAuthenticated] = useState(false);
    const [authError, setAuthError] = useState('');
    const [authLoading, setAuthLoading] = useState(false);

    const [file, setFile] = useState<File | null>(null);
    const [uploading, setUploading] = useState(false);
    const [uploadMsg, setUploadMsg] = useState('');
    const [uploadError, setUploadError] = useState('');

    const [status, setStatus] = useState<DbStatus | null>(null);
    const [statusLoading, setStatusLoading] = useState(false);

    const [bsDataStatus, setBsDataStatus] = useState<BsDataStatus | null>(null);
    const [bsDataImporting, setBsDataImporting] = useState(false);
    const [bsDataMsg, setBsDataMsg] = useState('');
    const [bsDataError, setBsDataError] = useState('');

    const handleLogin = async (e: React.FormEvent) => {
        e.preventDefault();
        setAuthLoading(true);
        setAuthError('');
        try {
            const res = await fetch('/api/admin/verify', {
                method: 'POST',
                headers: { 'X-Admin-Password': password },
            });
            if (res.ok) {
                setAuthenticated(true);
                loadStatus();
                loadBsDataStatus();
            } else {
                setAuthError('Invalid password. Please try again.');
            }
        } catch {
            setAuthError('Connection error.');
        } finally {
            setAuthLoading(false);
        }
    };

    const loadStatus = async () => {
        setStatusLoading(true);
        try {
            const res = await fetch('/api/admin/status', {
                headers: { 'X-Admin-Password': password },
            });
            if (res.ok) setStatus(await res.json());
        } finally {
            setStatusLoading(false);
        }
    };

    const loadBsDataStatus = async () => {
        try {
            const res = await fetch('/api/bsdata-admin/status', {
                headers: { 'X-Admin-Password': password },
            });
            if (res.ok) setBsDataStatus(await res.json());
        } catch {
            // ignore
        }
    };

    const handleUpload = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!file) return;
        setUploading(true);
        setUploadMsg('');
        setUploadError('');
        const formData = new FormData();
        formData.append('file', file);
        try {
            const res = await fetch('/api/admin/upload', {
                method: 'POST',
                headers: { 'X-Admin-Password': password },
                body: formData,
            });
            const data = await res.json();
            if (res.ok) {
                setUploadMsg(data.message ?? 'Upload successful!');
                loadStatus();
            } else {
                setUploadError(data.title ?? data.message ?? 'Upload failed.');
            }
        } catch (err) {
            setUploadError('Upload error: ' + String(err));
        } finally {
            setUploading(false);
        }
    };

    const handleBsDataImport = async () => {
        setBsDataImporting(true);
        setBsDataMsg('');
        setBsDataError('');
        try {
            const res = await fetch('/api/bsdata-admin/import', {
                method: 'POST',
                headers: { 'X-Admin-Password': password },
            });
            const data = await res.json();
            if (res.ok) {
                setBsDataMsg(data.message ?? 'Import successful!');
                loadBsDataStatus();
            } else {
                setBsDataError(data.title ?? data.message ?? 'Import failed.');
            }
        } catch (err) {
            setBsDataError('Import error: ' + String(err));
        } finally {
            setBsDataImporting(false);
        }
    };

    if (!authenticated) {
        return (
            <div style={styles.page}>
                <div style={styles.loginBox}>
                    <h2 style={styles.title}>🔐 Admin Panel</h2>
                    <p style={styles.hint}>Enter admin password to access the admin area.</p>
                    <form onSubmit={handleLogin} style={styles.form}>
                        <input
                            type="password"
                            placeholder="Admin password"
                            value={password}
                            onChange={e => setPassword(e.target.value)}
                            style={styles.input}
                            autoFocus
                        />
                        <button type="submit" disabled={authLoading || !password} style={styles.btn}>
                            {authLoading ? 'Verifying...' : 'Login'}
                        </button>
                    </form>
                    {authError && <p style={styles.error}>{authError}</p>}
                </div>
            </div>
        );
    }

    return (
        <div style={styles.page}>
            <h2 style={styles.title}>🛡️ Admin Panel</h2>

            <div style={styles.section}>
                <h3 style={styles.sectionTitle}>Database Status</h3>
                {statusLoading && <p style={styles.hint}>Loading...</p>}
                {status && (
                    <div style={styles.statusGrid}>
                        <StatusCard label="Last Update" value={status.lastUpdate ?? 'Never'} />
                        <StatusCard label="Factions" value={status.factions} />
                        <StatusCard label="Datasheets" value={status.datasheets} />
                        <StatusCard label="Abilities" value={status.abilities} />
                        <StatusCard label="Detachments" value={status.detachments} />
                        <StatusCard label="Stratagems" value={status.stratagems} />
                        <StatusCard label="Enhancements" value={status.enhancements} />
                        <StatusCard label="Sources" value={status.sources} />
                    </div>
                )}
                <button style={{ ...styles.btn, marginTop: 12 }} onClick={loadStatus}>Refresh Status</button>
            </div>

            <div style={styles.section}>
                <h3 style={styles.sectionTitle}>Upload Data</h3>
                <p style={styles.hint}>
                    Upload a <strong>Data.rar</strong> file containing the CSV data files.
                    This will replace all existing data in the database.
                </p>
                <form onSubmit={handleUpload} style={styles.form}>
                    <input
                        type="file"
                        accept=".rar"
                        onChange={e => setFile(e.target.files?.[0] ?? null)}
                        style={styles.fileInput}
                    />
                    <button type="submit" disabled={uploading || !file} style={styles.btn}>
                        {uploading ? 'Uploading...' : 'Upload & Import'}
                    </button>
                </form>
                {uploadMsg && <p style={styles.success}>{uploadMsg}</p>}
                {uploadError && <p style={styles.error}>{uploadError}</p>}
            </div>

            <div style={styles.section}>
                <h3 style={styles.sectionTitle}>BSData 40k Database</h3>
                <p style={styles.hint}>
                    Fetch and import data from the{' '}
                    <a href="https://github.com/BSData/wh40k-10e" target="_blank" rel="noopener noreferrer" style={styles.link}>
                        BSData/wh40k-10e
                    </a>{' '}
                    GitHub repository. This will replace all existing BSData records.
                </p>
                {bsDataStatus && (
                    <div style={{ ...styles.statusGrid, marginBottom: 16 }}>
                        <StatusCard label="Catalogues" value={bsDataStatus.catalogues} />
                        <StatusCard label="Units" value={bsDataStatus.units} />
                        <StatusCard label="Profiles" value={bsDataStatus.profiles} />
                    </div>
                )}
                <button
                    style={styles.btn}
                    onClick={handleBsDataImport}
                    disabled={bsDataImporting}
                >
                    {bsDataImporting ? 'Importing...' : '⬇ Получить данные wh40k-BSData'}
                </button>
                {bsDataMsg && <p style={styles.success}>{bsDataMsg}</p>}
                {bsDataError && <p style={styles.error}>{bsDataError}</p>}
            </div>
        </div>
    );
}

function StatusCard({ label, value }: { label: string; value: string | number }) {
    return (
        <div style={statusCardStyle}>
            <div style={{ color: '#aaa', fontSize: '0.8rem' }}>{label}</div>
            <div style={{ color: '#e8c170', fontWeight: 700, fontSize: '1.1rem', marginTop: 4 }}>{value}</div>
        </div>
    );
}

const statusCardStyle: React.CSSProperties = {
    background: '#0d0d1a',
    border: '1px solid #333',
    borderRadius: 6,
    padding: '12px 16px',
    minWidth: 120,
};

const styles: Record<string, React.CSSProperties> = {
    page: { maxWidth: 800, margin: '40px auto', padding: '0 24px' },
    loginBox: { maxWidth: 400, margin: '0 auto', background: '#1a1a2e', border: '1px solid #8b0000', borderRadius: 8, padding: 32 },
    title: { color: '#e8c170', marginBottom: 16 },
    sectionTitle: { color: '#e8c170', marginBottom: 12 },
    hint: { color: '#aaa', marginBottom: 12 },
    form: { display: 'flex', gap: 12, flexDirection: 'column' },
    input: { padding: '10px 14px', background: '#0d0d1a', border: '1px solid #8b0000', color: '#ccc', borderRadius: 4, fontSize: '1rem' },
    fileInput: { color: '#ccc' },
    btn: { padding: '10px 20px', background: '#8b0000', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer', fontSize: '1rem', fontWeight: 600 },
    error: { color: 'tomato', marginTop: 8 },
    success: { color: '#4caf50', marginTop: 8 },
    section: { background: '#1a1a2e', border: '1px solid #333', borderRadius: 8, padding: 24, marginBottom: 24 },
    statusGrid: { display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(130px, 1fr))', gap: 12 },
    link: { color: '#e8c170' },
};
