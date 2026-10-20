import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import api from '../api/axios';

export default function Dashboard() {
  const { t } = useTranslation();
  const [appMode, setAppMode] = useState(2);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchMode();
  }, []);

  const fetchMode = async () => {
    try {
      const res = await api.get('/settings/mode');
      setAppMode(res.data.mode);
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  };

  const handleModeChange = async (newMode) => {
    try {
      await api.post(`/settings/mode?mode=${newMode}`);
      fetchMode();
    } catch (e) {
      console.error(e);
    }
  };

  const handleClearCache = async () => {
    try {
      setLoading(true);
      await api.post('/settings/clear-cache');
      alert('Cache cleared successfully!');
    } catch (e) {
      console.error(e);
      alert('Failed to clear cache');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
        <h1 style={{ fontSize: '2rem' }}>{t('dashboard.title')}</h1>
        {!loading && (
          <div className="glass" style={{ padding: '0.5rem 1rem', display: 'flex', alignItems: 'center', gap: '1rem' }}>
            <span style={{ fontWeight: 'bold' }}>Global App Mode:</span>
            <select className="input" style={{ width: 'auto', padding: '0.4rem' }} value={appMode} onChange={(e) => handleModeChange(e.target.value)}>
              <option value={1}>Debug Mode</option>
              <option value={2}>Production Mode</option>
            </select>
            <button className="btn btn-primary" onClick={handleClearCache}>
              Clear Cache
            </button>
          </div>
        )}
      </div>
      <p style={{ color: 'var(--text-secondary)' }}>{t('dashboard.welcome')}</p>
      <p style={{ color: 'var(--text-secondary)' }}>{t('dashboard.selectNav')}</p>
      
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '2rem', marginTop: '2rem' }}>
        <div className="glass" style={{ padding: '2rem' }}>
          <h3 style={{ color: '#ec4899', marginBottom: '1rem' }}>{t('channels.title')}</h3>
          <p style={{ color: 'var(--text-secondary)', marginBottom: '1.5rem' }}>{t('dashboard.selectNav')}</p>
        </div>
        <div className="glass" style={{ padding: '2rem' }}>
          <h3 style={{ color: '#a855f7', marginBottom: '1rem' }}>{t('admins.title')}</h3>
          <p style={{ color: 'var(--text-secondary)', marginBottom: '1.5rem' }}>{t('dashboard.selectNav')}</p>
        </div>
      </div>
    </div>
  );
}
