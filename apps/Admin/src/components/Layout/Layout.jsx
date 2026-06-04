import { Outlet } from 'react-router-dom';
import { LgpdConsentGate } from '@/components/LgpdConsentGate';
import { SystemStatusBanner } from '@/components/SystemStatusBanner';
import { Header } from './Header';
import { Sidebar } from './Sidebar';

export function Layout() {
  return (
    <div className="app-shell">
      <Sidebar />
      <div className="app-main">
        <Header />
        <SystemStatusBanner />
        <main className="page-frame">
          <Outlet />
        </main>
        <LgpdConsentGate />
      </div>
    </div>
  );
}
