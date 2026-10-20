import React, { useState, useEffect } from 'react';
import api from '../api/axios';
import { useTranslation } from 'react-i18next';
import { Eye, CheckCircle, XCircle, Copy, ArrowDownLeft, ArrowUpRight } from 'lucide-react';

export default function Reports() {
  const [reports, setReports] = useState([]);
  const [loading, setLoading] = useState(true);
  const [selectedReport, setSelectedReport] = useState(null);
  const { t } = useTranslation();

  const fetchData = async () => {
    try {
      const res = await api.get('/reports');
      setReports(res.data);
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  const openModal = (report) => {
    setSelectedReport(report);
  };

  const closeModal = () => {
    setSelectedReport(null);
  };

  const getShortContent = (r) => {
    if (!r.metaDatas) return '-';
    const contentMeta = r.metaDatas.find(m => m.metaKey === 'content');
    if (contentMeta && contentMeta.metaValue) {
      const text = contentMeta.metaValue;
      return text.length > 30 ? text.substring(0, 30) + '...' : text;
    }
    return '-';
  };

  const getSenderId = (r) => {
    if (!r.metaDatas) return '';
    const s = r.metaDatas.find(m => m.metaKey === 'senderId');
    return s ? s.metaValue : '';
  };

  const copyLink = (r) => {
    const senderId = getSenderId(r);
    // Remove -100 from channel IDs if present to form a t.me/c/ link
    let chatStr = senderId;
    if (chatStr.startsWith('-100')) {
      chatStr = chatStr.substring(4);
    }
    const link = `https://t.me/c/${chatStr}/${r.sourceMessageId}`;
    navigator.clipboard.writeText(link);
    alert('لینک کپی شد: ' + link);
  };

  if (loading) {
    return <div style={{ padding: '2rem', textAlign: 'center' }}>Loading...</div>;
  }

  return (
    <div>
      <h1 style={{ marginBottom: '2rem' }}>گزارش ارسال‌ها</h1>

      <div className="glass" style={{ padding: '2rem', overflowX: 'auto' }}>
        <table className="table">
          <thead>
            <tr>
              <th>آیدی ارسال</th>
              <th>نوع</th>
              <th>خلاصه پیام</th>
              <th>ادمین / فرستنده</th>
              <th>کانال مقصد</th>
              <th>وضعیت ارسال</th>
              <th>آیدی مبدا</th>
              <th>تاریخ</th>
              <th>عملیات</th>
            </tr>
          </thead>
          <tbody>
            {reports.map((r) => (
              <tr key={r.id}>
                <td>{r.id}</td>
                <td>
                  {r.inOutType === 1 ? (
                    <span style={{ color: '#60a5fa', display: 'flex', alignItems: 'center', gap: '0.25rem' }}>
                      <ArrowDownLeft size={16} /> ورودی
                    </span>
                  ) : (
                    <span style={{ color: '#f472b6', display: 'flex', alignItems: 'center', gap: '0.25rem' }}>
                      <ArrowUpRight size={16} /> خروجی
                    </span>
                  )}
                </td>
                <td style={{ maxWidth: '200px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={getShortContent(r)}>
                  {getShortContent(r)}
                </td>
                <td>{r.inOutType === 1 ? getSenderId(r) : r.adminUser}</td>
                <td>{r.inOutType === 1 ? '-' : r.channelName}</td>
                <td>
                  {r.isSuccessful ? (
                    <span style={{ color: '#4ade80', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                      <CheckCircle size={18} /> موفق
                    </span>
                  ) : (
                    <span style={{ color: '#f87171', display: 'flex', alignItems: 'center', gap: '0.5rem' }} title={r.errorLog}>
                      <XCircle size={18} /> خطا
                    </span>
                  )}
                </td>
                <td style={{ direction: 'ltr', textAlign: 'center' }}>{r.sourceMessageId || '-'}</td>
                <td style={{ direction: 'ltr' }}>{new Date(r.dateInsert).toLocaleString('fa-IR')}</td>
                <td>
                  <div style={{ display: 'flex', gap: '0.5rem' }}>
                    <button className="btn btn-secondary" style={{ padding: '0.5rem' }} onClick={() => openModal(r)} title="جزئیات متادیتا">
                      <Eye size={18} />
                    </button>
                    {r.sourceMessageId && (
                      <button className="btn btn-secondary" style={{ padding: '0.5rem' }} onClick={() => copyLink(r)} title="کپی لینک تلگرام">
                        <Copy size={18} />
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
            {reports.length === 0 && (
              <tr>
                <td colSpan="9" style={{ textAlign: 'center', padding: '2rem' }}>هیچ گزارشی یافت نشد</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Modal */}
      {selectedReport && (
        <div style={{
          position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
          backgroundColor: 'rgba(0,0,0,0.5)', zIndex: 50,
          display: 'flex', alignItems: 'center', justifyContent: 'center'
        }}>
          <div className="glass" style={{ width: '90%', maxWidth: '600px', padding: '2rem', maxHeight: '90vh', overflowY: 'auto' }}>
            <h2 style={{ marginBottom: '1.5rem', display: 'flex', justifyContent: 'space-between' }}>
              جزئیات و متادیتا ارسال #{selectedReport.id}
              <button onClick={closeModal} style={{ background: 'transparent', border: 'none', color: '#f87171', cursor: 'pointer' }}>بستن</button>
            </h2>

            {selectedReport.errorLog && !selectedReport.isSuccessful && (
              <div style={{ marginBottom: '1rem', padding: '1rem', backgroundColor: 'rgba(248,113,113,0.1)', color: '#f87171', borderRadius: '0.5rem' }}>
                <strong>خطا:</strong> {selectedReport.errorLog}
              </div>
            )}

            <h3 style={{ borderBottom: '1px solid rgba(255,255,255,0.1)', paddingBottom: '0.5rem' }}>لیست متادیتاها:</h3>
            {selectedReport.metaDatas && selectedReport.metaDatas.length > 0 ? (
              <ul style={{ listStyle: 'none', padding: 0, marginTop: '1rem' }}>
                {selectedReport.metaDatas.map((meta, idx) => (
                  <li key={idx} style={{ padding: '0.75rem', background: 'rgba(255,255,255,0.05)', marginBottom: '0.5rem', borderRadius: '0.5rem' }}>
                    <strong style={{ color: '#8b5cf6' }}>{meta.metaKey}: </strong>
                    <span style={{ wordBreak: 'break-word', whiteSpace: 'pre-wrap' }}>{meta.metaValue}</span>
                  </li>
                ))}
              </ul>
            ) : (
              <p style={{ marginTop: '1rem', color: '#9ca3af' }}>هیچ متادیتایی ثبت نشده است.</p>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
