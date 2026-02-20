import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { NavBar } from './components/NavBar';
import { Home } from './pages/Home';
import { Factions } from './pages/Factions';
import { Datasheets } from './pages/Datasheets';
import { Detachments } from './pages/Detachments';
import { Stratagems } from './pages/Stratagems';
import { Enhancements } from './pages/Enhancements';
import { Admin } from './pages/Admin';
import './App.css';

function App() {
    return (
        <BrowserRouter>
            <NavBar />
            <Routes>
                <Route path="/" element={<Home />} />
                <Route path="/factions" element={<Factions />} />
                <Route path="/datasheets" element={<Datasheets />} />
                <Route path="/detachments" element={<Detachments />} />
                <Route path="/stratagems" element={<Stratagems />} />
                <Route path="/enhancements" element={<Enhancements />} />
                <Route path="/admin" element={<Admin />} />
            </Routes>
        </BrowserRouter>
    );
}

export default App;