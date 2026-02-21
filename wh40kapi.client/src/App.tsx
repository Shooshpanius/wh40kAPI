import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { NavBar } from './components/NavBar';
import { StartPage } from './pages/StartPage';
import { Home } from './pages/Home';
import { Factions } from './pages/Factions';
import { Datasheets } from './pages/Datasheets';
import { Detachments } from './pages/Detachments';
import { Stratagems } from './pages/Stratagems';
import { Enhancements } from './pages/Enhancements';
import { Admin } from './pages/Admin';
import { BSData40k } from './pages/BSData40k';
import { BSDataKillTeam } from './pages/BSDataKillTeam';
import './App.css';

function App() {
    return (
        <BrowserRouter>
            <NavBar />
            <Routes>
                <Route path="/" element={<StartPage />} />
                <Route path="/wahapedia" element={<Home />} />
                <Route path="/factions" element={<Factions />} />
                <Route path="/datasheets" element={<Datasheets />} />
                <Route path="/detachments" element={<Detachments />} />
                <Route path="/stratagems" element={<Stratagems />} />
                <Route path="/enhancements" element={<Enhancements />} />
                <Route path="/admin" element={<Admin />} />
                <Route path="/bsdata-40k" element={<BSData40k />} />
                <Route path="/bsdata-killteam" element={<BSDataKillTeam />} />
            </Routes>
        </BrowserRouter>
    );
}

export default App;