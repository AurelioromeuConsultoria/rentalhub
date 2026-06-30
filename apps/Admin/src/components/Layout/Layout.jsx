import { Outlet } from 'react-router-dom';
import { LgpdConsentGate } from '@/components/LgpdConsentGate';
import { SupportModeBanner } from '@/components/SupportModeBanner';
import { SystemStatusBanner } from '@/components/SystemStatusBanner';
import { Header } from './Header';
import { Sidebar } from './Sidebar';

export function Layout() {
  return (
    <div className="app-shell">
      <Sidebar />
      <div className="app-main">
        <Header />
        <SupportModeBanner />
        <SystemStatusBanner />
        <main className="page-frame">
          <Outlet />
        </main>
        <LgpdConsentGate />
      </div>
    </div>
  );
}
