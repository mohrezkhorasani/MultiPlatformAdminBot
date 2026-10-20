import React, { useState, useEffect } from 'react';
import api from '../api/axios';
import { Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';

export default function Channels() {
  const [channels, setChannels] = useState([]);
  const [loading, setLoading] = useState(true);
  const { t } = useTranslation();

  // Form
  const [platformName, setPlatformName] = useState('Telegram');
  const [tokens, setTokens] = useState([]);
  const [botTokenId, setBotTokenId] = useState('');
  const [targetId, setTargetId] = useState('');

  const fetchChannels = async () => {
    try {
      const [chanRes, tokRes] = await Promise.all([
        api.get('/channel'),
        api.get('/token')
      ]);
      setChannels(chanRes.data);
      setTokens(tokRes.data);
      if (tokRes.data.length > 0 && !botTokenId) {
        setBotTokenId(tokRes.data[0].id);
      }
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchChannels();
  }, []);

  const handleAdd = async (e) => {
    e.preventDefault();
    try {
      await api.post('/channel', { platformName, botTokenId, targetChannelId: targetId, isActive: true });
      setTargetId('');
      fetchChannels();
    } catch (e) {
      console.error(e);
    }
  };

  const handleDelete = async (id) => {
    if(!window.confirm(t('common.confirmDelete'))) return;
    try {
      await api.delete(`/channel/${id}`);
      fetchChannels();
    } catch (e) {
      console.error(e);
    }
  };

  const handleUpdateType = async (channel, newType) => {
    try {
      await api.put(`/channel/${channel.id}`, { ...channel, type: parseInt(newType) });
      fetchChannels();
    } catch (e) {
      console.error(e);
    }
  };

  return (
    <div>
      <h1 style={{ marginBottom: '2rem' }}>{t('channels.title')}</h1>
      
      <div className="glass" style={{ padding: '2rem', marginBottom: '2rem' }}>
        <h3 style={{ marginBottom: '1rem' }}>{t('channels.addTitle')}</h3>
        <form onSubmit={handleAdd} style={{ display: 'grid', gridTemplateColumns: '1fr 2fr 1fr auto', gap: '1rem', alignItems: 'center' }}>
          <select className="input" value={platformName} onChange={e => setPlatformName(e.target.value)}>
            <option>Telegram</option>
            <option>Bale</option>
            <option>Eitaa</option>
            <option>Rubika</option>
          </select>
          <select className="input" value={botTokenId} onChange={e => setBotTokenId(e.target.value)} required>
            <option value="">{t('channels.selectToken')}</option>
            {tokens.filter(tok => tok.platformName === platformName).map(tok => (
              <option key={tok.id} value={tok.id}>{tok.tokenValue.substring(0, 15)}...</option>
            ))}
          </select>
          <input className="input" placeholder={t('channels.targetId')} value={targetId} onChange={e => setTargetId(e.target.value)} required />
          <button type="submit" className="btn"><Plus size={18}/> {t('common.add')}</button>
        </form>
      </div>

      <div className="glass" style={{ overflow: 'hidden' }}>
        <table>
          <thead>
            <tr>
              <th>{t('channels.id')}</th>
              <th>{t('channels.platform')}</th>
              <th>{t('channels.tokenId')}</th>
              <th>{t('channels.targetId')}</th>
              <th>{t('common.status')}</th>
              <th>Type (1=Debug, 2=Prod)</th>
              <th>{t('common.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {channels.map(c => (
              <tr key={c.id}>
                <td>{c.id}</td>
                <td><span style={{ color: 'var(--primary)', fontWeight: 'bold' }}>{c.platformName}</span></td>
                <td>{c.botTokenId}</td>
                <td>{c.targetChannelId}</td>
                <td>{c.isActive ? t('common.active') : t('common.disabled')}</td>
                <td>
                  <select className="input" style={{ padding: '0.2rem', width: 'auto' }} value={c.type} onChange={(e) => handleUpdateType(c, e.target.value)}>
                    <option value={1}>Debug</option>
                    <option value={2}>Production</option>
                  </select>
                </td>
                <td>
                  <button className="btn btn-danger" style={{ padding: '0.4rem 0.8rem' }} onClick={() => handleDelete(c.id)}>
                    <Trash2 size={16}/>
                  </button>
                </td>
              </tr>
            ))}
            {channels.length === 0 && !loading && (
              <tr><td colSpan="6" style={{ textAlign: 'center', padding: '2rem' }}>{t('common.noRecords')}</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
