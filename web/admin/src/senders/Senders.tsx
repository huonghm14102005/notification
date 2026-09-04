import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../shared/types';
import { Status, Time } from '../notifications/Status';
import { ConfirmDialog } from '../notifications/ConfirmDialog';

export type Sender = {
  id: string;
  tenantId?: string;
  key?: string;
  senderKey?: string;
  channel?: string;
  host: string;
  port: number;
  secure: boolean;
  username: string;
  fromEmail: string;
  fromName?: string;
  isDefault: boolean;
  status: 'active' | 'disabled';
  verifiedAt?: string | null;
  createdAt: string;
  updatedAt: string;
};

export type SenderPage = {
  items: Sender[];
  nextCursor?: string;
};

interface SmtpPreset {
  id: 'resend' | 'gmail' | 'sendgrid' | 'custom';
  name: string;
  badge: string;
  key: string;
  host: string;
  port: number;
  secure: boolean;
  username: string;
  fromEmail: string;
  fromName: string;
  passwordPlaceholder: string;
  note: string;
  warning?: string;
}

const PRESETS: Record<string, SmtpPreset> = {
  resend: {
    id: 'resend',
    name: 'Resend (Khuyên dùng trên Cloud / Render)',
    badge: 'Resend HTTPS 443',
    key: 'resend-primary',
    host: 'smtp.resend.com',
    port: 587,
    secure: false,
    username: 'resend',
    fromEmail: 'onboarding@resend.dev',
    fromName: 'Notification Service',
    passwordPlaceholder: 're_... (API Key lấy tại resend.com/api-keys)',
    note: '⚡ Tự động định tuyến qua Resend REST API (HTTPS Cổng 443). Hoạt động hoàn hảo trên Render Free Tier mà không bị chặn cổng mạng SMTP!',
  },
  gmail: {
    id: 'gmail',
    name: 'Google Gmail (SMTP)',
    badge: 'Gmail SMTP',
    key: 'gmail-mailer',
    host: 'smtp.gmail.com',
    port: 587,
    secure: false,
    username: '',
    fromEmail: '',
    fromName: 'Hệ thống thông báo',
    passwordPlaceholder: '16 ký tự Mật khẩu ứng dụng (Google App Password)',
    note: '🔑 Cần bật 2FA trên Google Account và tạo Mật khẩu ứng dụng 16 ký tự.',
    warning: '⚠️ Lưu ý: Render Free Tier chặn các cổng SMTP trực tiếp (587, 465, 25). Nếu chạy trên Render Free, kết nối Gmail SMTP sẽ bị lỗi Timeout (504).',
  },
  sendgrid: {
    id: 'sendgrid',
    name: 'SendGrid (Twilio)',
    badge: 'SendGrid SMTP',
    key: 'sendgrid-mailer',
    host: 'smtp.sendgrid.net',
    port: 587,
    secure: false,
    username: 'apikey',
    fromEmail: '',
    fromName: 'SendGrid Mailer',
    passwordPlaceholder: 'SG.xxx... (SendGrid API Key)',
    note: 'Đảm bảo From Email đã được xác thực (Single Sender Verification) trong SendGrid.',
    warning: '⚠️ Lưu ý: Cổng 587/465 có thể bị nhà cung cấp Cloud (Render) chặn.',
  },
  custom: {
    id: 'custom',
    name: 'Máy chủ SMTP Tùy chỉnh',
    badge: 'Custom SMTP',
    key: 'smtp-custom',
    host: '',
    port: 587,
    secure: false,
    username: '',
    fromEmail: '',
    fromName: '',
    passwordPlaceholder: 'Mật khẩu hoặc Token kết nối SMTP',
    note: 'Điền thông số máy chủ thư doanh nghiệp, Mailgun, Amazon SES hoặc Postmark.',
  },
};

function getProviderInfo(host: string) {
  const h = (host || '').toLowerCase();
  if (h.includes('resend.com')) {
    return {
      type: 'resend',
      label: 'Resend',
      badge: '⚡ Resend (HTTPS 443)',
      color: '#0284c7',
      bg: '#e0f2fe',
      borderColor: '#bae6fd',
      isResend: true,
      description: 'Định tuyến tự động qua Resend REST API (HTTPS Cổng 443) — Chống chặn mạng trên Cloud / Render',
    };
  }
  if (h.includes('gmail.com')) {
    return {
      type: 'gmail',
      label: 'Gmail',
      badge: 'Gmail SMTP',
      color: '#b91c1c',
      bg: '#fee2e2',
      borderColor: '#fecaca',
      isResend: false,
      description: 'Máy chủ Google Mail SMTP (Cổng 587 STARTTLS / 465 SSL)',
    };
  }
  if (h.includes('sendgrid.net')) {
    return {
      type: 'sendgrid',
      label: 'SendGrid',
      badge: 'SendGrid SMTP',
      color: '#15803d',
      bg: '#dcfce7',
      borderColor: '#bbf7d0',
      isResend: false,
      description: 'Máy chủ Twilio SendGrid SMTP',
    };
  }
  return {
    type: 'custom',
    label: 'Custom SMTP',
    badge: 'Custom SMTP',
    color: '#475569',
    bg: '#f1f5f9',
    borderColor: '#e2e8f0',
    isResend: false,
    description: 'Máy chủ SMTP TCP tiêu chuẩn',
  };
}

export function SenderList() {
  const auth = useAuth();
  const qc = useQueryClient();
  const [showCreate, setShowCreate] = useState(false);
  const [deletingSender, setDeletingSender] = useState<Sender | null>(null);
  const [error, setError] = useState('');

  // Form state with default preset: Resend
  const [selectedPreset, setSelectedPreset] = useState<'resend' | 'gmail' | 'sendgrid' | 'custom'>('resend');
  const [formKey, setFormKey] = useState(PRESETS.resend.key);
  const [formHost, setFormHost] = useState(PRESETS.resend.host);
  const [formPort, setFormPort] = useState(PRESETS.resend.port);
  const [formSecure, setFormSecure] = useState(PRESETS.resend.secure);
  const [formUsername, setFormUsername] = useState(PRESETS.resend.username);
  const [formPassword, setFormPassword] = useState('');
  const [formFromEmail, setFormFromEmail] = useState(PRESETS.resend.fromEmail);
  const [formFromName, setFormFromName] = useState(PRESETS.resend.fromName);

  const applyPreset = (presetKey: 'resend' | 'gmail' | 'sendgrid' | 'custom') => {
    setSelectedPreset(presetKey);
    const p = PRESETS[presetKey];
    setFormKey(p.key);
    setFormHost(p.host);
    setFormPort(p.port);
    setFormSecure(p.secure);
    setFormUsername(p.username);
    if (p.fromEmail) setFormFromEmail(p.fromEmail);
    if (p.fromName) setFormFromName(p.fromName);
  };

  const q = useQuery({
    queryKey: ['senders'],
    queryFn: () => auth.request<SenderPage>('/v1/senders'),
  });

  const create = useMutation({
    mutationFn: (data: {
      key: string;
      host: string;
      port: number;
      secure: boolean;
      username: string;
      password: string;
      fromEmail: string;
      fromName?: string;
    }) =>
      auth.request<Sender>('/v1/senders', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      }),
    onSuccess: () => {
      setShowCreate(false);
      setFormPassword('');
      qc.invalidateQueries({ queryKey: ['senders'] });
    },
    onError: (e) => {
      if (e instanceof ApiError) {
        setError(e.detailMessage ? `Lỗi (${e.code}): ${e.detailMessage}` : `Lỗi máy chủ: ${e.code}`);
      } else {
        setError('Không thể tạo sender. Vui lòng kiểm tra lại kết nối.');
      }
    },
  });

  const deleteSenderMutation = useMutation({
    mutationFn: (senderId: string) =>
      auth.request<void>(`/v1/senders/${senderId}`, {
        method: 'DELETE',
      }),
    onSuccess: () => {
      setDeletingSender(null);
      setError('');
      qc.invalidateQueries({ queryKey: ['senders'] });
    },
    onError: (e) => {
      setError(e instanceof ApiError ? (e.detailMessage || e.code) : 'Không thể xóa cấu hình sender.');
      setDeletingSender(null);
    },
  });

  return (
    <section>
      <header className="page-head">
        <div>
          <div className="eyebrow">KÊNH EMAIL</div>
          <h1>Cấu hình Máy Chủ Gửi Thư (Senders)</h1>
          <p>Quản lý các tài khoản máy chủ SMTP / Resend HTTPS dùng để gửi email thông báo.</p>
        </div>
        <button
          onClick={() => {
            setError('');
            applyPreset('resend');
            setShowCreate(true);
          }}
        >
          + Thêm Máy Chủ Gửi Thư
        </button>
      </header>

      {showCreate && (
        <div className="modal-backdrop">
          <div className="modal" style={{ maxWidth: '640px' }}>
            <div className="modal-head">
              <div>
                <h2>Thêm máy chủ gửi thư mới</h2>
                <p style={{ margin: '4px 0 0', fontSize: '0.85rem', color: 'var(--muted)' }}>
                  Chọn mẫu cấu hình nhanh hoặc điền thông số máy chủ tùy chỉnh.
                </p>
              </div>
              <button className="ghost" onClick={() => setShowCreate(false)}>✕</button>
            </div>

            {/* Quick Preset Selector */}
            <div style={{ marginBottom: '16px' }}>
              <div style={{ fontSize: '0.8rem', fontWeight: 700, color: 'var(--muted)', marginBottom: '8px' }}>
                CHỌN NHÀ CUNG CẤP / MẪU CẤU HÌNH NHANH:
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: '8px' }}>
                {(['resend', 'gmail', 'sendgrid', 'custom'] as const).map((pk) => {
                  const p = PRESETS[pk];
                  const active = selectedPreset === pk;
                  return (
                    <button
                      key={pk}
                      type="button"
                      className="ghost"
                      onClick={() => applyPreset(pk)}
                      style={{
                        padding: '8px 10px',
                        fontSize: '0.82rem',
                        textAlign: 'center',
                        justifyContent: 'center',
                        fontWeight: active ? 700 : 500,
                        borderColor: active ? 'var(--ink)' : 'var(--line)',
                        background: active ? '#17221e' : 'white',
                        color: active ? 'white' : 'inherit',
                      }}
                    >
                      {pk === 'resend' ? '⚡ ' : ''}{p.badge}
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Preset Tips Banner */}
            {PRESETS[selectedPreset].note && (
              <div
                style={{
                  padding: '10px 14px',
                  background: selectedPreset === 'resend' ? '#f0f9ff' : '#f8faf9',
                  border: `1px solid ${selectedPreset === 'resend' ? '#bae6fd' : 'var(--line)'}`,
                  borderRadius: '8px',
                  fontSize: '0.85rem',
                  color: selectedPreset === 'resend' ? '#0369a1' : 'inherit',
                  marginBottom: '14px',
                  lineHeight: 1.45,
                }}
              >
                {PRESETS[selectedPreset].note}
              </div>
            )}

            {PRESETS[selectedPreset].warning && (
              <div
                style={{
                  padding: '10px 14px',
                  background: '#fffbeb',
                  border: '1px solid #fde68a',
                  borderRadius: '8px',
                  fontSize: '0.85rem',
                  color: '#92400e',
                  marginBottom: '14px',
                  lineHeight: 1.45,
                }}
              >
                {PRESETS[selectedPreset].warning}
              </div>
            )}

            <form
              onSubmit={(e) => {
                e.preventDefault();
                create.mutate({
                  key: formKey.trim().toLowerCase(),
                  host: formHost.trim().toLowerCase(),
                  port: Number(formPort),
                  secure: formSecure,
                  username: formUsername.trim(),
                  password: formPassword,
                  fromEmail: formFromEmail.trim().toLowerCase(),
                  fromName: formFromName.trim() || undefined,
                });
              }}
            >
              <label>
                Mã định danh Sender (Sender Key)
                <input
                  value={formKey}
                  onChange={(e) => setFormKey(e.target.value)}
                  placeholder="vd: resend-primary, transactional, marketing"
                  required
                  maxLength={50}
                />
              </label>

              <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '10px' }}>
                <label>
                  Host
                  <input
                    value={formHost}
                    onChange={(e) => setFormHost(e.target.value)}
                    placeholder="smtp.resend.com / smtp.gmail.com"
                    required
                  />
                </label>
                <label>
                  Port
                  <input
                    type="number"
                    value={formPort}
                    onChange={(e) => setFormPort(Number(e.target.value))}
                    required
                  />
                </label>
              </div>

              <label>
                Chế độ mã hóa (SSL/TLS)
                <select
                  value={String(formSecure)}
                  onChange={(e) => setFormSecure(e.target.value === 'true')}
                >
                  <option value="false">STARTTLS / Plain (Khuyến nghị cổng 587)</option>
                  <option value="true">SSL/TLS Implicit (Cổng 465)</option>
                </select>
              </label>

              <label>
                Username
                <input
                  value={formUsername}
                  onChange={(e) => setFormUsername(e.target.value)}
                  placeholder={selectedPreset === 'resend' ? 'resend' : 'Username hoặc email đăng nhập'}
                  required
                />
              </label>

              <label>
                Mật khẩu / API Key
                <input
                  type="password"
                  value={formPassword}
                  onChange={(e) => setFormPassword(e.target.value)}
                  placeholder={PRESETS[selectedPreset].passwordPlaceholder}
                  required
                />
              </label>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                <label>
                  From Email
                  <input
                    type="email"
                    value={formFromEmail}
                    onChange={(e) => setFormFromEmail(e.target.value)}
                    placeholder="onboarding@resend.dev / noreply@domain.com"
                    required
                  />
                </label>
                <label>
                  From Name
                  <input
                    value={formFromName}
                    onChange={(e) => setFormFromName(e.target.value)}
                    placeholder="Hệ thống thông báo"
                  />
                </label>
              </div>

              {error && <div className="error" role="alert">{error}</div>}

              <div className="modal-actions">
                <button type="button" className="ghost" onClick={() => setShowCreate(false)}>Hủy</button>
                <button disabled={create.isPending}>
                  {create.isPending ? 'Đang tạo…' : 'Tạo Máy Chủ Gửi Thư'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {q.isLoading ? (
        <p>Đang tải danh sách máy chủ gửi thư…</p>
      ) : q.error ? (
        <div className="error">Không tải được danh sách sender. Vui lòng kiểm tra quyền Admin hoặc kết nối mạng.</div>
      ) : q.data?.items.length === 0 ? (
        <div className="empty">
          <h2>Chưa có cấu hình máy chủ gửi thư nào</h2>
          <p>Thêm cấu hình Resend hoặc SMTP sender đầu tiên để kích hoạt tính năng gửi email thông báo.</p>
          <button
            style={{ marginTop: '16px' }}
            onClick={() => {
              setError('');
              applyPreset('resend');
              setShowCreate(true);
            }}
          >
            + Tạo Sender với Resend ngay
          </button>
        </div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Sender Key</th>
                <th>Nhà cung cấp / Máy chủ</th>
                <th>Người gửi (From)</th>
                <th>Mặc định</th>
                <th>Kiểm thử</th>
                <th>Trạng thái</th>
                <th>Ngày tạo</th>
                <th style={{ textAlign: 'right' }}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {q.data?.items.map((s) => {
                const keyName = s.key || s.senderKey || s.id;
                const prov = getProviderInfo(s.host);
                return (
                  <tr key={s.id}>
                    <td>
                      <Link
                        to={`/senders/${s.id}`}
                        className="id"
                        style={{ fontWeight: 700, color: 'var(--primary, #0ea5e9)', textDecoration: 'none' }}
                      >
                        {keyName}
                      </Link>
                      <small style={{ fontFamily: 'monospace' }}>{s.id.slice(0, 8)}…</small>
                    </td>
                    <td>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px', marginBottom: '2px' }}>
                        <span
                          className="badge"
                          style={{
                            background: prov.bg,
                            color: prov.color,
                            border: `1px solid ${prov.borderColor}`,
                            fontSize: '0.7rem',
                            padding: '2px 6px',
                          }}
                        >
                          {prov.badge}
                        </span>
                      </div>
                      <div style={{ fontSize: '0.85rem' }}>
                        <code>{s.host}:{s.port}</code> {s.secure ? '🔒' : ''}
                      </div>
                      <small>User: {s.username}</small>
                    </td>
                    <td>
                      <div style={{ fontWeight: 500 }}>{s.fromName || '—'}</div>
                      <small style={{ color: 'var(--muted)' }}>&lt;{s.fromEmail}&gt;</small>
                    </td>
                    <td>
                      {s.isDefault ? (
                        <span className="badge badge-default">★ Mặc định</span>
                      ) : (
                        <span style={{ color: 'var(--muted)' }}>—</span>
                      )}
                    </td>
                    <td>
                      {s.verifiedAt ? (
                        <div>
                          <span
                            className="badge"
                            style={{
                              background: '#dcfce7',
                              color: '#15803d',
                              border: '1px solid #bbf7d0',
                              fontSize: '0.72rem',
                            }}
                          >
                            ✓ Đã kiểm thử
                          </span>
                          <small><Time value={s.verifiedAt} /></small>
                        </div>
                      ) : (
                        <span
                          className="badge"
                          style={{
                            background: '#fef3c7',
                            color: '#b45309',
                            border: '1px solid #fde68a',
                            fontSize: '0.72rem',
                          }}
                        >
                          ⚠ Chưa kiểm thử
                        </span>
                      )}
                    </td>
                    <td>
                      <Status value={s.status} />
                    </td>
                    <td><Time value={s.createdAt} /></td>
                    <td style={{ textAlign: 'right' }}>
                      <Link
                        to={`/senders/${s.id}`}
                        className="ghost"
                        style={{
                          padding: '5px 12px',
                          fontSize: '13px',
                          textDecoration: 'none',
                          borderRadius: '6px',
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '4px',
                        }}
                      >
                        ⚙️ Chi tiết / Sửa
                      </Link>
                      <button
                        type="button"
                        className="ghost"
                        style={{
                          padding: '5px 10px',
                          fontSize: '13px',
                          borderRadius: '6px',
                          marginLeft: '6px',
                          color: '#ef4444',
                          border: '1px solid #fee2e2',
                        }}
                        onClick={() => setDeletingSender(s)}
                        title="Xóa cấu hình này"
                      >
                        🗑️ Xóa
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {deletingSender && (
        <ConfirmDialog
          open={Boolean(deletingSender)}
          title="Xác nhận xóa cấu hình Máy Chủ Gửi Thư"
          busy={deleteSenderMutation.isPending}
          onCancel={() => setDeletingSender(null)}
          onConfirm={() => deleteSenderMutation.mutate(deletingSender.id)}
        >
          Bạn có chắc chắn muốn xóa cấu hình sender <strong>"{deletingSender.key || deletingSender.id}"</strong> ({deletingSender.host})?
          <br />
          <span style={{ fontSize: '0.85rem', color: 'var(--muted)', display: 'block', marginTop: '6px' }}>
            * Nếu cấu hình này chưa từng gửi tin, hệ thống sẽ xóa hoàn toàn. Nếu đã có tin nhắn liên kết, hệ thống sẽ vô hiệu hóa (disabled) an toàn để bảo vệ dữ liệu lịch sử.
          </span>
        </ConfirmDialog>
      )}
    </section>
  );
}

export function SenderDetail() {
  const { id } = useParams();
  const auth = useAuth();
  const nav = useNavigate();
  const qc = useQueryClient();

  const [showTestModal, setShowTestModal] = useState(false);
  const [testEmail, setTestEmail] = useState('');
  const [testResult, setTestResult] = useState<{
    success: boolean;
    sent?: boolean;
    recipientEmail?: string;
    verifiedAt?: string;
    message?: string;
    code?: string;
    detailMessage?: string;
  } | null>(null);

  const [actionError, setActionError] = useState('');
  const [successNotice, setSuccessNotice] = useState('');
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [showDisableConfirm, setShowDisableConfirm] = useState(false);

  // Edit form state
  const [editHost, setEditHost] = useState('');
  const [editPort, setEditPort] = useState(587);
  const [editSecure, setEditSecure] = useState(false);
  const [editUsername, setEditUsername] = useState('');
  const [editPassword, setEditPassword] = useState('');
  const [editFromEmail, setEditFromEmail] = useState('');
  const [editFromName, setEditFromName] = useState('');
  const [formInitialized, setFormInitialized] = useState(false);

  const q = useQuery({
    queryKey: ['senders'],
    queryFn: () => auth.request<SenderPage>('/v1/senders'),
  });

  const currentSender = q.data?.items.find((s) => s.id === id);

  // Synchronize form fields once sender loads
  if (currentSender && !formInitialized) {
    setEditHost(currentSender.host);
    setEditPort(currentSender.port);
    setEditSecure(currentSender.secure);
    setEditUsername(currentSender.username);
    setEditFromEmail(currentSender.fromEmail);
    setEditFromName(currentSender.fromName || '');
    setFormInitialized(true);
  }

  const applyEditPreset = (presetKey: 'resend' | 'gmail' | 'sendgrid') => {
    const p = PRESETS[presetKey];
    setEditHost(p.host);
    setEditPort(p.port);
    setEditSecure(p.secure);
    setEditUsername(p.username);
    if (p.fromEmail) setEditFromEmail(p.fromEmail);
    if (p.fromName) setEditFromName(p.fromName);
  };

  const updateMutation = useMutation({
    mutationFn: (data: Partial<Sender & { password?: string }>) =>
      auth.request<Sender>(`/v1/senders/${id}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      }),
    onSuccess: () => {
      setSuccessNotice('Đã cập nhật cấu hình máy chủ gửi thư thành công!');
      setEditPassword('');
      setActionError('');
      setTimeout(() => setSuccessNotice(''), 4000);
      qc.invalidateQueries({ queryKey: ['senders'] });
    },
    onError: (e) => {
      if (e instanceof ApiError) {
        setActionError(e.detailMessage ? `Lỗi (${e.code}): ${e.detailMessage}` : `Lỗi cập nhật: ${e.code}`);
      } else {
        setActionError('Cập nhật thất bại. Vui lòng kiểm tra lại kết nối.');
      }
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () =>
      auth.request<void>(`/v1/senders/${id}`, {
        method: 'DELETE',
      }),
    onSuccess: () => {
      setShowDeleteConfirm(false);
      qc.invalidateQueries({ queryKey: ['senders'] });
      nav('/senders');
    },
    onError: (e) => {
      setShowDeleteConfirm(false);
      if (e instanceof ApiError) {
        setActionError(e.detailMessage ? `Lỗi (${e.code}): ${e.detailMessage}` : `Xóa cấu hình thất bại: ${e.code}`);
      } else {
        setActionError('Xóa cấu hình thất bại.');
      }
    },
  });

  const disableMutation = useMutation({
    mutationFn: () =>
      auth.request<void>(`/v1/senders/${id}/disable`, {
        method: 'POST',
      }),
    onSuccess: () => {
      setShowDisableConfirm(false);
      qc.invalidateQueries({ queryKey: ['senders'] });
      setSuccessNotice('Đã chuyển trạng thái máy chủ gửi thư sang vô hiệu hóa (disabled).');
      setTimeout(() => setSuccessNotice(''), 4000);
    },
    onError: (e) => {
      setShowDisableConfirm(false);
      if (e instanceof ApiError) {
        setActionError(e.detailMessage ? `Lỗi (${e.code}): ${e.detailMessage}` : `Vô hiệu hóa thất bại: ${e.code}`);
      } else {
        setActionError('Vô hiệu hóa thất bại.');
      }
    },
  });

  const testSendMutation = useMutation({
    mutationFn: (recipientEmail: string) =>
      auth.request<{ sent: boolean; senderId: string; recipientEmail: string; verifiedAt: string }>(
        `/v1/senders/${id}/test`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ recipientEmail }),
        }
      ),
    onSuccess: (data) => {
      setTestResult({
        success: true,
        sent: data.sent,
        recipientEmail: data.recipientEmail,
        verifiedAt: data.verifiedAt,
        message: 'Gửi thư thử nghiệm thành công! Kết nối và xác thực hoạt động hoàn hảo.',
      });
      qc.invalidateQueries({ queryKey: ['senders'] });
    },
    onError: (e) => {
      if (e instanceof ApiError) {
        setTestResult({
          success: false,
          code: e.code,
          detailMessage: e.detailMessage,
          message:
            e.code === 'SMTP_TEST_TIMEOUT'
              ? 'Hết thời gian chờ kết nối SMTP (Gateway Timeout - 504).'
              : e.code === 'SMTP_TEST_FAILED'
              ? 'Kiểm thử gửi thư thất bại (Bad Gateway - 502).'
              : `Lỗi kiểm tra (${e.code}).`,
        });
      } else {
        setTestResult({
          success: false,
          message: 'Không thể kết nối hoặc gửi thử nghiệm. Vui lòng kiểm tra lại cấu hình.',
        });
      }
    },
  });

  if (q.isLoading) return <p>Đang tải chi tiết cấu hình…</p>;
  if (!currentSender) return <div className="error">Không tìm thấy thông tin cấu hình sender này.</div>;

  const s = currentSender;
  const keyName = s.key || s.senderKey || s.id;
  const prov = getProviderInfo(s.host);

  return (
    <section>
      <button className="back ghost" onClick={() => nav('/senders')}>← Quay lại danh sách Sender</button>

      <header className="page-head">
        <div>
          <div className="eyebrow">CHI TIẾT MÁY CHỦ GỬI THƯ</div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <h1>{keyName}</h1>
            <span
              className="badge"
              style={{
                background: prov.bg,
                color: prov.color,
                border: `1px solid ${prov.borderColor}`,
                fontSize: '0.8rem',
                padding: '3px 8px',
              }}
            >
              {prov.badge}
            </span>
            {s.isDefault && <span className="badge badge-default">★ Mặc định</span>}
          </div>
          <p>Máy chủ: <code>{s.host}:{s.port}</code> | Người gửi: <strong>{s.fromEmail}</strong></p>
        </div>

        <div className="actions">
          <button
            onClick={() => {
              setTestResult(null);
              setTestEmail('');
              setShowTestModal(true);
            }}
            style={{ background: 'var(--green)' }}
          >
            ✉ Gửi thư thử nghiệm
          </button>
          {!s.isDefault && s.status === 'active' && (
            <button
              className="ghost"
              disabled={updateMutation.isPending}
              onClick={() => updateMutation.mutate({ isDefault: true })}
            >
              Đặt làm mặc định
            </button>
          )}
          {s.status === 'active' && (
            <button
              className="ghost"
              disabled={disableMutation.isPending}
              onClick={() => setShowDisableConfirm(true)}
              style={{ color: '#b45309', border: '1px solid #fde68a' }}
            >
              Vô hiệu hóa
            </button>
          )}
          <button
            className="danger"
            disabled={deleteMutation.isPending}
            onClick={() => setShowDeleteConfirm(true)}
          >
            🗑️ Xóa cấu hình
          </button>
        </div>
      </header>

      {/* Cloud Routing Advisory Banner */}
      {prov.isResend ? (
        <div
          style={{
            padding: '12px 16px',
            background: '#f0f9ff',
            border: '1px solid #bae6fd',
            borderRadius: '8px',
            color: '#0369a1',
            marginBottom: '20px',
            fontSize: '0.88rem',
            display: 'flex',
            alignItems: 'center',
            gap: '10px',
          }}
        >
          <span style={{ fontSize: '1.2rem' }}>⚡</span>
          <div>
            <strong>Đã kích hoạt chế độ Resend HTTPS Port 443</strong>:
            Yêu cầu gửi thư từ Render Backend được tự động định tuyến qua HTTPS REST API (Port 443), miễn nhiễm với việc Render chặn cổng SMTP TCP 587/465.
          </div>
        </div>
      ) : (
        <div
          style={{
            padding: '12px 16px',
            background: '#fffbeb',
            border: '1px solid #fde68a',
            borderRadius: '8px',
            color: '#92400e',
            marginBottom: '20px',
            fontSize: '0.88rem',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: '12px',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <span style={{ fontSize: '1.2rem' }}>⚠️</span>
            <div>
              <strong>Lưu ý về mạng Cloud (Render Free Tier)</strong>:
              Nếu backend đang chạy trên Render Free Tier, các cổng SMTP TCP (587, 465, 25) sẽ bị chặn dẫn đến lỗi <code>SMTP_TEST_TIMEOUT (504)</code>. Bạn có thể chuyển nhanh cấu hình sang Resend để chạy qua cổng HTTPS 443.
            </div>
          </div>
          <button
            type="button"
            className="ghost"
            style={{ flexShrink: 0, fontSize: '0.8rem', padding: '5px 10px', background: 'white' }}
            onClick={() => applyEditPreset('resend')}
          >
            Điền nhanh thông số Resend
          </button>
        </div>
      )}

      {successNotice && <div className="success">{successNotice}</div>}
      {actionError && <div className="error">{actionError}</div>}

      {/* Test Email Modal */}
      {showTestModal && (
        <div className="modal-backdrop">
          <div className="modal" style={{ maxWidth: '560px' }}>
            <div className="modal-head">
              <div>
                <h2>Kiểm thử gửi thư (SMTP Test)</h2>
                <p style={{ margin: '4px 0 0', fontSize: '0.85rem', color: 'var(--muted)' }}>
                  Gửi một email kiểm tra kết nối trực tiếp từ máy chủ backend tới địa chỉ người nhận.
                </p>
              </div>
              <button className="ghost" onClick={() => setShowTestModal(false)}>✕</button>
            </div>

            {/* Resend Free Tier Notice */}
            {prov.isResend && s.fromEmail.includes('resend.dev') && (
              <div
                style={{
                  padding: '12px 14px',
                  background: '#fef3c7',
                  border: '1px solid #fde68a',
                  borderRadius: '8px',
                  fontSize: '0.85rem',
                  color: '#b45309',
                  lineHeight: 1.45,
                }}
              >
                <strong>💡 Lưu ý tài khoản Resend Free:</strong>
                <p style={{ margin: '4px 0 0' }}>
                  Với tên miền mặc định <code>onboarding@resend.dev</code>, Resend <strong>CHỈ CHO PHÉP</strong> gửi tới chính địa chỉ email bạn đã dùng để đăng ký tài khoản Resend (ví dụ: <code>huong102145@st.vimaru.edu.vn</code>). Nếu nhập email khác sẽ bị Resend từ chối với mã 403 Forbidden.
                </p>
              </div>
            )}

            <form
              onSubmit={(e) => {
                e.preventDefault();
                testSendMutation.mutate(testEmail.trim());
              }}
            >
              <label>
                Địa chỉ email người nhận thử nghiệm
                <input
                  type="email"
                  placeholder={prov.isResend ? 'Nhập email đã đăng ký tài khoản Resend...' : 'recipient@example.com'}
                  value={testEmail}
                  onChange={(e) => setTestEmail(e.target.value)}
                  required
                />
              </label>

              {testResult && (
                <div
                  className={testResult.success ? 'success' : 'error'}
                  style={{ lineHeight: 1.5, wordBreak: 'break-word' }}
                >
                  <div style={{ fontWeight: 700, marginBottom: '4px' }}>
                    {testResult.success ? '✓ Thành công!' : `✕ Thất bại (${testResult.code || 'ERROR'})`}
                  </div>
                  <div>{testResult.message}</div>

                  {testResult.verifiedAt && (
                    <div style={{ marginTop: '6px', fontSize: '0.82rem' }}>
                      Thời gian xác thực: <Time value={testResult.verifiedAt} />
                    </div>
                  )}

                  {testResult.detailMessage && (
                    <div
                      style={{
                        marginTop: '8px',
                        padding: '8px 10px',
                        background: 'rgba(0,0,0,0.05)',
                        borderRadius: '6px',
                        fontFamily: 'monospace',
                        fontSize: '0.82rem',
                      }}
                    >
                      {testResult.detailMessage}
                    </div>
                  )}

                  {!testResult.success && testResult.code === 'SMTP_TEST_TIMEOUT' && (
                    <div style={{ marginTop: '8px', fontSize: '0.82rem', fontStyle: 'italic' }}>
                      💡 Gợi ý: Render Free Tier đang chặn cổng SMTP ra ngoài. Hãy đổi sang dùng Resend (Host: smtp.resend.com, Username: resend, Password: API Key re_...) để gửi qua cổng HTTPS 443.
                    </div>
                  )}

                  {!testResult.success && testResult.detailMessage?.includes('only send testing emails to your own email address') && (
                    <div style={{ marginTop: '8px', fontSize: '0.82rem', fontStyle: 'italic' }}>
                      💡 Gợi ý: Hãy nhập đúng địa chỉ email mà bạn đã dùng để đăng ký tài khoản tại resend.com.
                    </div>
                  )}
                </div>
              )}

              <div className="modal-actions">
                <button type="button" className="ghost" onClick={() => setShowTestModal(false)}>
                  Đóng
                </button>
                <button disabled={testSendMutation.isPending}>
                  {testSendMutation.isPending ? 'Đang gửi kiểm tra…' : 'Gửi thử ngay'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      <div className="grid">
        {/* Current Configuration Card */}
        <article className="card">
          <h2>Thông tin cấu hình hiện tại</h2>
          <dl>
            <dt>Mã Sender Key</dt>
            <dd><code>{keyName}</code></dd>

            <dt>Nhà cung cấp</dt>
            <dd>
              <span
                className="badge"
                style={{
                  background: prov.bg,
                  color: prov.color,
                  border: `1px solid ${prov.borderColor}`,
                  fontSize: '0.75rem',
                }}
              >
                {prov.badge}
              </span>
            </dd>

            <dt>Trạng thái</dt>
            <dd><Status value={s.status} /></dd>

            <dt>Mặc định</dt>
            <dd>{s.isDefault ? <span className="badge badge-default">★ Có</span> : 'Không'}</dd>

            <dt>Xác thực (Test)</dt>
            <dd>
              {s.verifiedAt ? (
                <span style={{ color: '#16a34a', fontWeight: 600 }}>
                  ✓ Đã kiểm thử (<Time value={s.verifiedAt} />)
                </span>
              ) : (
                <span style={{ color: '#d97706', fontWeight: 600 }}>
                  ⚠ Chưa được kiểm thử
                </span>
              )}
            </dd>

            <dt>Máy chủ & Cổng</dt>
            <dd><code>{s.host}:{s.port}</code></dd>

            <dt>Mã hóa SSL/TLS</dt>
            <dd>{s.secure ? 'SSL/TLS Implicit (Cổng 465)' : 'STARTTLS / Plain'}</dd>

            <dt>Username</dt>
            <dd>{s.username}</dd>

            <dt>Mật khẩu</dt>
            <dd style={{ color: 'var(--muted)', fontSize: '0.85rem' }}>
              •••••••• (Đã mã hóa AES-256-GCM trong DB)
            </dd>

            <dt>From Email</dt>
            <dd>{s.fromEmail}</dd>

            <dt>From Name</dt>
            <dd>{s.fromName || '—'}</dd>

            <dt>Ngày tạo</dt>
            <dd><Time value={s.createdAt} /></dd>

            <dt>Cập nhật lần cuối</dt>
            <dd><Time value={s.updatedAt} /></dd>
          </dl>
        </article>

        {/* Edit Configuration Card */}
        <article className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <h2 style={{ margin: 0 }}>Chỉnh sửa thông số Sender</h2>
            <div style={{ display: 'flex', gap: '6px' }}>
              <button
                type="button"
                className="ghost"
                style={{ padding: '3px 8px', fontSize: '0.75rem' }}
                onClick={() => applyEditPreset('resend')}
              >
                Mẫu Resend
              </button>
              <button
                type="button"
                className="ghost"
                style={{ padding: '3px 8px', fontSize: '0.75rem' }}
                onClick={() => applyEditPreset('gmail')}
              >
                Mẫu Gmail
              </button>
            </div>
          </div>

          <form
            onSubmit={(e) => {
              e.preventDefault();
              const payload: Record<string, unknown> = {
                host: editHost.trim().toLowerCase(),
                port: Number(editPort),
                secure: editSecure,
                username: editUsername.trim(),
                fromEmail: editFromEmail.trim().toLowerCase(),
                fromName: editFromName.trim() || '',
              };
              if (editPassword.trim()) {
                payload.password = editPassword.trim();
              }
              updateMutation.mutate(payload as Partial<Sender & { password?: string }>);
            }}
          >
            <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '10px' }}>
              <label>
                Host
                <input
                  value={editHost}
                  onChange={(e) => setEditHost(e.target.value)}
                  placeholder="smtp.resend.com / smtp.gmail.com"
                  required
                />
              </label>
              <label>
                Port
                <input
                  type="number"
                  value={editPort}
                  onChange={(e) => setEditPort(Number(e.target.value))}
                  required
                />
              </label>
            </div>

            <label>
              Chế độ mã hóa (SSL/TLS)
              <select
                value={String(editSecure)}
                onChange={(e) => setEditSecure(e.target.value === 'true')}
              >
                <option value="false">STARTTLS / Plain (Cổng 587)</option>
                <option value="true">SSL/TLS Implicit (Cổng 465)</option>
              </select>
            </label>

            <label>
              Username
              <input
                value={editUsername}
                onChange={(e) => setEditUsername(e.target.value)}
                placeholder="resend / email"
                required
              />
            </label>

            <div
              style={{
                background: '#f8faf9',
                border: '1px dashed var(--line)',
                padding: '12px 14px',
                borderRadius: '8px',
              }}
            >
              <label style={{ margin: 0 }}>
                <span style={{ display: 'flex', justifyContent: 'space-between' }}>
                  <span>Mật khẩu mới / API Key mới</span>
                  <span style={{ fontWeight: 400, color: 'var(--muted)', fontSize: '0.78rem' }}>
                    (Bỏ trống nếu giữ nguyên mật khẩu cũ)
                  </span>
                </span>
                <input
                  type="password"
                  value={editPassword}
                  onChange={(e) => setEditPassword(e.target.value)}
                  placeholder="Nhập API Key mới (re_...) hoặc mật khẩu mới nếu muốn đổi..."
                  style={{ marginTop: '6px' }}
                />
              </label>
              <div style={{ marginTop: '6px', fontSize: '0.78rem', color: 'var(--muted)', lineHeight: 1.4 }}>
                💡 <strong>Lưu ý:</strong> Khi chuyển đổi nhà cung cấp (ví dụ từ Gmail sang Resend), <strong>BẮT BUỘC</strong> phải nhập API Key mới vào ô trên trước khi nhấn Lưu.
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
              <label>
                From Email
                <input
                  type="email"
                  value={editFromEmail}
                  onChange={(e) => setEditFromEmail(e.target.value)}
                  placeholder="onboarding@resend.dev"
                  required
                />
              </label>
              <label>
                From Name
                <input
                  value={editFromName}
                  onChange={(e) => setEditFromName(e.target.value)}
                  placeholder="Hệ thống thông báo"
                />
              </label>
            </div>

            <div className="modal-actions" style={{ marginTop: '20px' }}>
              <button disabled={updateMutation.isPending}>
                {updateMutation.isPending ? 'Đang lưu…' : 'Lưu Cập Nhật Cấu Hình'}
              </button>
            </div>
          </form>
        </article>
      </div>

      {showDisableConfirm && (
        <ConfirmDialog
          open={showDisableConfirm}
          title="Xác nhận vô hiệu hóa Máy Chủ Gửi Thư"
          busy={disableMutation.isPending}
          onCancel={() => setShowDisableConfirm(false)}
          onConfirm={() => disableMutation.mutate()}
        >
          Bạn có chắc chắn muốn vô hiệu hóa máy chủ gửi thư <strong>"{keyName}"</strong>? Máy chủ sẽ chuyển sang trạng thái <code>disabled</code> và không nhận nhiệm vụ gửi email mới.
        </ConfirmDialog>
      )}

      {showDeleteConfirm && (
        <ConfirmDialog
          open={showDeleteConfirm}
          title="Xác nhận xóa Máy Chủ Gửi Thư"
          busy={deleteMutation.isPending}
          onCancel={() => setShowDeleteConfirm(false)}
          onConfirm={() => deleteMutation.mutate()}
        >
          Bạn có chắc chắn muốn xóa cấu hình máy chủ gửi thư <strong>"{keyName}"</strong> ({s.host})?
          <br />
          <span style={{ fontSize: '0.85rem', color: 'var(--muted)', display: 'block', marginTop: '6px' }}>
            * Nếu cấu hình này chưa từng gửi tin, hệ thống sẽ xóa vĩnh viễn. Nếu đã có tin nhắn trong lịch sử gửi, hệ thống sẽ vô hiệu hóa (disabled) an toàn để tránh mất dữ liệu liên kết.
          </span>
        </ConfirmDialog>
      )}
    </section>
  );
}
