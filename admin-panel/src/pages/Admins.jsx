import React, { useState, useEffect } from 'react';
import api from '../api/axios';
import { UserPlus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';

export default function Admins() {
  const [admins, setAdmins] = useState([]);
  const [channels, setChannels] = useState([]);
  const [loading, setLoading] = useState(true);
  const { t } = useTranslation();

  // Form
  const [adminId, setAdminId] = useState('');
  const [explain, setExplain] = useState('');
  const [password, setPassword] = useState('');
  const [footerText, setFooterText] = useState('');
  const [selectedChannels, setSelectedChannels] = useState([]);
  const [editingId, setEditingId] = useState(null);

  const fetchData = async () => {
    try {
      const [admRes, chnRes] = await Promise.all([
        api.get('/admin'),
        api.get('/channel')
      ]);
      setAdmins(admRes.data);
      setChannels(chnRes.data);
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  const handleCheckbox = (id) => {
    if(selectedChannels.includes(id)) {
      setSelectedChannels(selectedChannels.filter(c => c !== id));
    } else {
      setSelectedChannels([...selectedChannels, id]);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      if (editingId) {
        await api.put(`/admin/${editingId}`, { 
          explain, 
          password,
          footerText,
          channelIds: selectedChannels 
        });
      } else {
        await api.post('/admin', { 
          id: Number(adminId), 
          explain, 
          password,
          footerText,
          channelIds: selectedChannels 
        });
      }
      resetForm();
      fetchData();
    } catch (e) {
      console.error(e);
    }
  };

  const resetForm = () => {
    setAdminId('');
    setExplain('');
    setPassword('');
    setFooterText('');
    setSelectedChannels([]);
    setEditingId(null);
  };

  const handleEdit = (admin) => {
    setEditingId(admin.id);
    setAdminId(admin.id.toString());
    setExplain(admin.explain);
    setPassword('');
    setFooterText(admin.footerText || '');
    setSelectedChannels(admin.channelIds || []);
  };

  const handleDelete = async (id) => {
    if(!window.confirm(t('common.confirmDelete'))) return;
    try {
      await api.delete(`/admin/${id}`);
      fetchData();
    } catch (e) {
      console.error(e);
    }
  };

  return (
    <div>
      <h1 style={{ marginBottom: '2rem' }}>{t('admins.title')}</h1>
      
      <div className="glass" style={{ padding: '2rem', marginBottom: '2rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
          <h3>{editingId ? 'Edit Admin' : t('admins.addTitle')}</h3>
          {editingId && (
            <button className="btn" style={{ background: 'transparent', border: '1px solid var(--surface-border)' }} onClick={resetForm}>{t('common.cancel')}</button>
          )}
        </div>
        <form onSubmit={handleSubmit} style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '1rem', alignItems: 'start' }}>
          
          <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <input className="input" placeholder={t('admins.id')} value={adminId} onChange={e => setAdminId(e.target.value)} required type="number" disabled={!!editingId} />
            <input className="input" placeholder={t('admins.name')} value={explain} onChange={e => setExplain(e.target.value)} required />
            <input className="input" placeholder={editingId ? "New Password" : t('admins.password')} value={password} onChange={e => setPassword(e.target.value)} type="password" required={!editingId} />
            <input className="input" placeholder="متن فوتر (اختیاری)" value={footerText} onChange={e => setFooterText(e.target.value)} />
          </div>

          <div className="glass" style={{ padding: '1rem', background: 'rgba(0,0,0,0.2)', height: '100%' }}>
            <h4 style={{ marginBottom: '0.5rem', color: 'var(--text-secondary)' }}>{t('admins.assignedChannels')}</h4>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', maxHeight: '150px', overflowY: 'auto' }}>
              {channels.map(c => (
                <label key={c.id} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer' }}>
                  <input type="checkbox" checked={selectedChannels.includes(c.id)} onChange={() => handleCheckbox(c.id)} />
                  {c.platformName} - {c.targetChannelId || 'No Target'}
                </label>
              ))}
            </div>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-start', height: '100%' }}>
            <button type="submit" className="btn" style={{ padding: '1rem 2rem' }}><UserPlus size={20}/> {editingId ? t('common.save') : t('common.add')}</button>
          </div>
        </form>
      </div>

      <div className="glass" style={{ overflow: 'hidden' }}>
        <table>
          <thead>
            <tr>
              <th>{t('admins.id')}</th>
              <th>{t('admins.name')}</th>
              <th>متن فوتر</th>
              <th>{t('admins.assignedChannels')}</th>
              <th>{t('common.status')}</th>
              <th>{t('common.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {admins.map(a => (
              <tr key={a.id}>
                <td>{a.id}</td>
                <td>{a.explain}</td>
                <td>{a.footerText ? a.footerText : '-'}</td>
                <td>
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem' }}>
                    {a.channelIds && a.channelIds.map(cId => {
                      const c = channels.find(x => x.id === cId);
                      return c ? (
                        <span key={c.id} style={{ background: 'rgba(255,255,255,0.1)', padding: '0.2rem 0.5rem', borderRadius: '4px', fontSize: '0.8rem' }}>
                          {c.platformName}
                        </span>
                      ) : null;
                    })}
                  </div>
                </td>
                <td>{a.status === 100 ? t('common.active') : t('common.disabled')}</td>
                <td>
                  <div style={{ display: 'flex', gap: '0.5rem' }}>
                    <button className="btn" style={{ padding: '0.4rem 0.8rem', background: 'var(--primary)' }} onClick={() => handleEdit(a)}>
                      Edit
                    </button>
                    <button className="btn btn-danger" style={{ padding: '0.4rem 0.8rem' }} onClick={() => handleDelete(a.id)}>
                      <Trash2 size={16}/>
                    </button>
                  </div>
                </td>
              </tr>
            ))}
            {admins.length === 0 && !loading && (
              <tr><td colSpan="5" style={{ textAlign: 'center', padding: '2rem' }}>{t('common.noRecords')}</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
