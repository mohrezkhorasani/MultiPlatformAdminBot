import React from 'react';
import { Outlet, Link, useNavigate, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { LayoutDashboard, Users, Key, Radio, LogOut, MessageSquare, FileText } from 'lucide-react';

export default function Layout() {
  const navigate = useNavigate();
  const location = useLocation();

  const { t, i18n } = useTranslation();

  const handleLogout = () => {
    localStorage.removeItem('admin_token');
    navigate('/login');
  };

  const toggleLanguage = () => {
    const nextLang = i18n.language === 'en' ? 'fa' : 'en';
    i18n.changeLanguage(nextLang);
    localStorage.setItem('app_language', nextLang);
  };

  const navs = [
    { name: t('nav.dashboard'), path: '/', icon: LayoutDashboard },
    { name: t('nav.channels'), path: '/channels', icon: MessageSquare },
    { name: t('nav.tokens'), path: '/tokens', icon: Key },
    { name: t('nav.admins'), path: '/admins', icon: Users },
    { name: 'گزارش ارسال‌ها', path: '/reports', icon: FileText },
  ];

  return (
    <div className="app-container">
      <div className="sidebar">
        <h2 style={{ padding: '1rem', color: 'var(--primary)', marginBottom: '1rem' }}>{t('nav.adminPanel')}</h2>
        {navs.map(nav => {
          const Icon = nav.icon;
          return (
            <Link 
              key={nav.path} 
              to={nav.path} 
              className={`sidebar-link ${location.pathname === nav.path ? 'active' : ''}`}
            >
              <Icon size={20} />
              {nav.name}
            </Link>
          );
        })}
        <div style={{ flex: 1 }} />
        <button className="sidebar-link" onClick={toggleLanguage} style={{ background: 'transparent', border: 'none', cursor: 'pointer', textAlign: i18n.language === 'fa' ? 'right' : 'left', width: '100%' }}>
          <span style={{ fontSize: '1.2rem', padding: '0 4px' }}>🌐</span>
          {i18n.language === 'fa' ? 'English' : 'فارسی'}
        </button>
        <button className="sidebar-link" onClick={handleLogout} style={{ background: 'transparent', border: 'none', cursor: 'pointer', textAlign: i18n.language === 'fa' ? 'right' : 'left', width: '100%', color: 'var(--danger)' }}>
          <LogOut size={20} />
          {t('nav.logout')}
        </button>
      </div>
      <div className="main-content">
        <Outlet />
      </div>
    </div>
  );
}
