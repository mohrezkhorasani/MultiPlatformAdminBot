import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Lock, User } from 'lucide-react';
import api from '../api/axios';

export default function Login() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();

  const handleLogin = async (e) => {
    e.preventDefault();
    try {
      const res = await api.post('/auth/login', { username: Number(username), password });
      localStorage.setItem('admin_token', res.data.token);
      navigate('/');
    } catch (err) {
      setError(t('login.error'));
    }
  };

  const isRtl = i18n.language === 'fa';

  return (
    <div className="auth-wrapper">
      <div className="glass auth-box">
        <h1 className="auth-title">{t('login.title')}</h1>
        <p style={{ textAlign: 'center', marginBottom: '2rem', color: 'var(--text-secondary)' }}>{t('login.subtitle')}</p>
        <form onSubmit={handleLogin} style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
          {error && <div style={{ color: 'var(--danger)', textAlign: 'center' }}>{error}</div>}
          <div style={{ position: 'relative' }}>
            <User size={20} style={{ position: 'absolute', top: 12, [isRtl ? 'right' : 'left']: 12, color: 'var(--text-secondary)' }} />
            <input 
              type="text" 
              className="input" 
              placeholder={t('login.username')}
              value={username} 
              onChange={e => setUsername(e.target.value)} 
              style={{ paddingLeft: isRtl ? '1rem' : '2.5rem', paddingRight: isRtl ? '2.5rem' : '1rem' }}
            />
          </div>
          <div style={{ position: 'relative' }}>
            <Lock size={20} style={{ position: 'absolute', top: 12, [isRtl ? 'right' : 'left']: 12, color: 'var(--text-secondary)' }} />
            <input 
              type="password" 
              className="input" 
              placeholder={t('login.password')}
              value={password} 
              onChange={e => setPassword(e.target.value)} 
              style={{ paddingLeft: isRtl ? '1rem' : '2.5rem', paddingRight: isRtl ? '2.5rem' : '1rem' }}
            />
          </div>
          <button type="submit" className="btn" style={{ justifyContent: 'center', marginTop: '1rem' }}>
            {t('login.button')}
          </button>
        </form>
      </div>
    </div>
  );
}
