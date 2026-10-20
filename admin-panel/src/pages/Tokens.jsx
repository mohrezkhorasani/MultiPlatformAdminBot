import React, { useState, useEffect } from 'react';
import api from '../api/axios';
import { Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';

export default function Tokens() {
  const [tokens, setTokens] = useState([]);
  const [loading, setLoading] = useState(true);
  const { t } = useTranslation();

  // Form
  const [platformName, setPlatformName] = useState('Telegram');
  const [tokenValue, setTokenValue] = useState('');

  const fetchTokens = async () => {
    try {
      const res = await api.get('/token');
      setTokens(res.data);
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchTokens();
  }, []);

  const handleAdd = async (e) => {
    e.preventDefault();
    try {
      await api.post('/token', { platformName, tokenValue, isActive: true });
      setTokenValue('');
      fetchTokens();
    } catch (e) {
      console.error(e);
    }
  };

  const handleDelete = async (id) => {
    if(!window.confirm(t('common.confirmDelete'))) return;
    try {
      await api.delete(`/token/${id}`);
      fetchTokens();
    } catch (e) {
      if (e.response && e.response.status === 400) {
        alert(e.response.data.message);
      } else {
        console.error(e);
      }
    }
  };

  return (
    <div>
      <h1 style={{ marginBottom: '2rem' }}>{t('tokens.title')}</h1>
      
      <div className="glass" style={{ padding: '2rem', marginBottom: '2rem' }}>
        <h3 style={{ marginBottom: '1rem' }}>{t('tokens.addTitle')}</h3>
        <form onSubmit={handleAdd} style={{ display: 'grid', gridTemplateColumns: '1fr 3fr auto', gap: '1rem', alignItems: 'center' }}>
          <select className="input" value={platformName} onChange={e => setPlatformName(e.target.value)}>
            <option>Telegram</option>
            <option>Bale</option>
            <option>Eitaa</option>
            <option>Rubika</option>
          </select>
          <input className="input" placeholder={t('tokens.tokenValue')} value={tokenValue} onChange={e => setTokenValue(e.target.value)} required />
          <button type="submit" className="btn"><Plus size={18}/> {t('common.add')}</button>
        </form>
      </div>

      <div className="glass" style={{ overflow: 'hidden' }}>
        <table>
          <thead>
            <tr>
              <th>{t('tokens.id')}</th>
              <th>{t('tokens.platform')}</th>
              <th>{t('tokens.tokenValue')}</th>
              <th>{t('common.status')}</th>
              <th>{t('common.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {tokens.map(tData => (
              <tr key={tData.id}>
                <td>{tData.id}</td>
                <td><span style={{ color: 'var(--primary)', fontWeight: 'bold' }}>{tData.platformName}</span></td>
                <td style={{ maxWidth: 300, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{tData.tokenValue}</td>
                <td>{tData.isActive ? t('common.active') : t('common.disabled')}</td>
                <td>
                  <button className="btn btn-danger" style={{ padding: '0.4rem 0.8rem' }} onClick={() => handleDelete(tData.id)}>
                    <Trash2 size={16}/>
                  </button>
                </td>
              </tr>
            ))}
            {tokens.length === 0 && !loading && (
              <tr><td colSpan="5" style={{ textAlign: 'center', padding: '2rem' }}>{t('common.noRecords')}</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
