import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../shared/types';
import { Status, Time } from '../notifications/Status';

type Sender = {
  id: string;
  tenantId: string;
  senderKey: string;
  host: string;
  port: number;
  secure: boolean;
  username: string;
  fromEmail: string;
  fromName?: string;
  isDefault: boolean;
  status: 'active' | 'disabled';
  createdAt: string;
  updatedAt: string;
};

type SenderPage = {
  items: Sender[];
  nextCursor?: string;
};

export function SenderList() {
  const auth = useAuth();
  const qc = useQueryClient();
  const [showCreate, setShowCreate] = useState(false);
  const [error, setError] = useState('');

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
      qc.invalidateQueries({ queryKey: ['senders'] });
    },
    onError: (e) => {
      setError(e instanceof ApiError ? `Lỗi: ${e.code}` : 'Không thể tạo sender.');
    },
  });

  return (
    <section>
      <header className="page-head">
        <div>
          <div className="eyebrow">KÊNH EMAIL</div>
          <h1>Cấu hình SMTP Senders</h1>
          <p>Quản lý các tài khoản máy chủ SMTP dùng để gửi email thông báo.</p>
        </div>
        <button onClick={() => { setError(''); setShowCreate(true); }}>Thêm SMTP Sender</button>
      </header>

      {showCreate && (
        <div className="modal-backdrop">
          <div className="modal">
            <div className="modal-head">
              <h2>Thêm máy chủ SMTP mới</h2>
              <button className="ghost" onClick={() => setShowCreate(false)}>✕</button>
            </div>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                const d = new FormData(e.currentTarget);
                create.mutate({
                  key: String(d.get('key')),
                  host: String(d.get('host')),
                  port: Number(d.get('port')),
                  secure: d.get('secure') === 'true',
                  username: String(d.get('username')),
                  password: String(d.get('password')),
                  fromEmail: String(d.get('fromEmail')),
                  fromName: String(d.get('fromName') || ''),
                });
              }}
            >
              <label>
                Mã định danh Sender (Sender Key)
                <input name="key" placeholder="vd: default, transactional, marketing" required maxLength={50} />
              </label>
              <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '10px' }}>
                <label>
                  SMTP Host
                  <input name="host" placeholder="smtp.mailgun.org / mail.domain.com" required />
                </label>
                <label>
                  Port
                  <input name="port" type="number" defaultValue={587} required />
                </label>
              </div>
              <label>
                Chế độ mã hóa (SSL/TLS)
                <select name="secure" defaultValue="false">
                  <option value="false">STARTTLS / Plain (Cổng 587 hoặc 25)</option>
                  <option value="true">SSL/TLS Implicit (Cổng 465)</option>
                </select>
              </label>
              <label>
                SMTP Username
                <input name="username" required />
              </label>
              <label>
                SMTP Password
                <input name="password" type="password" required />
              </label>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                <label>
                  From Email
                  <input name="fromEmail" type="email" placeholder="noreply@domain.com" required />
                </label>
                <label>
                  From Name
                  <input name="fromName" placeholder="Hệ thống thông báo" />
                </label>
              </div>

              {error && <div className="error" role="alert">{error}</div>}
              <div className="modal-actions">
                <button type="button" className="ghost" onClick={() => setShowCreate(false)}>Hủy</button>
                <button disabled={create.isPending}>Tạo Sender</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {q.isLoading ? (
        <p>Đang tải danh sách senders…</p>
      ) : q.error ? (
        <div className="error">Không tải được danh sách sender.</div>
      ) : q.data?.items.length === 0 ? (
        <div className="empty">
          <h2>Chưa có cấu hình SMTP nào</h2>
          <p>Thêm SMTP sender đầu tiên để kích hoạt tính năng gửi email.</p>
        </div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Sender Key</th>
                <th>Máy chủ SMTP</th>
                <th>Địa chỉ người gửi (From)</th>
                <th>Mặc định</th>
                <th>Trạng thái</th>
                <th>Ngày tạo</th>
                <th style={{ textAlign: 'right' }}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {q.data?.items.map((s) => (
                <tr key={s.id}>
                  <td>
                    <Link to={`/senders/${s.id}`} className="id" style={{ fontWeight: 600, color: 'var(--primary, #0ea5e9)' }}>{s.senderKey}</Link>
                    <small>{s.id}</small>
                  </td>
                  <td>
                    {s.host}:{s.port} {s.secure ? '🔒' : ''}
                    <small>User: {s.username}</small>
                  </td>
                  <td>
                    {s.fromName ? `${s.fromName} <${s.fromEmail}>` : s.fromEmail}
                  </td>
                  <td>
                    {s.isDefault ? (
                      <span className="badge badge-default">Mặc định</span>
                    ) : (
                      <span style={{ color: 'var(--muted)' }}>—</span>
                    )}
                  </td>
                  <td>
                    <Status value={s.status} />
                  </td>
                  <td><Time value={s.createdAt} /></td>
                  <td style={{ textAlign: 'right' }}>
                    <Link to={`/senders/${s.id}`} className="ghost" style={{ padding: '4px 10px', fontSize: '13px', textDecoration: 'none', border: '1px solid var(--border)', borderRadius: '6px', display: 'inline-block' }}>
                      ⚙️ Chi tiết / Sửa
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
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
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);
  const [actionError, setActionError] = useState('');
  const [successNotice, setSuccessNotice] = useState('');

  const q = useQuery({
    queryKey: ['senders'],
    queryFn: () => auth.request<SenderPage>('/v1/senders'),
  });

  const currentSender = q.data?.items.find((s) => s.id === id);

  const updateMutation = useMutation({
    mutationFn: (data: Partial<Sender & { password?: string }>) =>
      auth.request<Sender>(`/v1/senders/${id}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      }),
    onSuccess: () => {
      setSuccessNotice('Đã cập nhật cấu hình sender thành công!');
      setTimeout(() => setSuccessNotice(''), 3000);
      qc.invalidateQueries({ queryKey: ['senders'] });
    },
    onError: (e) => setActionError(e instanceof ApiError ? e.code : 'Cập nhật thất bại.'),
  });

  const disableMutation = useMutation({
    mutationFn: () =>
      auth.request<void>(`/v1/senders/${id}`, {
        method: 'DELETE',
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['senders'] });
      nav('/senders');
    },
    onError: (e) => setActionError(e instanceof ApiError ? e.code : 'Vô hiệu hóa thất bại.'),
  });

  const testSendMutation = useMutation({
    mutationFn: (recipientEmail: string) =>
      auth.request<{ success: boolean; messageId?: string }>(`/v1/senders/${id}/test`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ recipientEmail }),
      }),
    onSuccess: (data) => {
      setTestResult({
        success: true,
        message: `Gửi email thử thành công! Message ID: ${data.messageId || 'N/A'}`,
      });
    },
    onError: (e) => {
      setTestResult({
        success: false,
        message: e instanceof ApiError ? `Lỗi kiểm tra SMTP (${e.code})` : 'Gửi thử thất bại.',
      });
    },
  });

  if (q.isLoading) return <p>Đang tải chi tiết cấu hình…</p>;
  if (!currentSender) return <div className="error">Không tìm thấy thông tin cấu hình sender.</div>;

  const s = currentSender;

  return (
    <section>
      <button className="back ghost" onClick={() => nav('/senders')}>← Quay lại danh sách</button>

      <header className="page-head">
        <div>
          <div className="eyebrow">CHI TIẾT SENDER SMTP</div>
          <h1>{s.senderKey}</h1>
          <p>Máy chủ: <code>{s.host}:{s.port}</code></p>
        </div>
        <div className="actions">
          <button className="ghost" onClick={() => { setTestResult(null); setShowTestModal(true); }}>
            ✉ Gửi thử nghiệm
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
              className="danger"
              disabled={disableMutation.isPending}
              onClick={() => {
                if (confirm('Vô hiệu hóa cấu hình SMTP sender này?')) {
                  disableMutation.mutate();
                }
              }}
            >
              Vô hiệu hóa
            </button>
          )}
        </div>
      </header>

      {successNotice && <div className="success">{successNotice}</div>}
      {actionError && <div className="error">{actionError}</div>}

      {/* Test Email Modal */}
      {showTestModal && (
        <div className="modal-backdrop">
          <div className="modal">
            <div className="modal-head">
              <h2>Gửi thư thử nghiệm (SMTP Test)</h2>
              <button className="ghost" onClick={() => setShowTestModal(false)}>✕</button>
            </div>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                testSendMutation.mutate(testEmail);
              }}
            >
              <p>Gửi một email kiểm tra kết nối và cấu hình trực tiếp tới địa chỉ người nhận.</p>
              <label>
                Email người nhận thử nghiệm
                <input
                  type="email"
                  placeholder="recipient@example.com"
                  value={testEmail}
                  onChange={(e) => setTestEmail(e.target.value)}
                  required
                />
              </label>

              {testResult && (
                <div className={testResult.success ? 'success' : 'error'}>
                  {testResult.message}
                </div>
              )}

              <div className="modal-actions">
                <button type="button" className="ghost" onClick={() => setShowTestModal(false)}>Đóng</button>
                <button disabled={testSendMutation.isPending}>
                  {testSendMutation.isPending ? 'Đang gửi…' : 'Gửi thử ngay'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      <div className="grid">
        <article className="card">
          <h2>Thông tin cấu hình</h2>
          <dl>
            <dt>Mã Sender Key</dt>
            <dd><code>{s.senderKey}</code></dd>
            <dt>Trạng thái</dt>
            <dd><Status value={s.status} /></dd>
            <dt>Mặc định</dt>
            <dd>{s.isDefault ? <span className="badge badge-default">Có</span> : 'Không'}</dd>
            <dt>Mã hóa SSL/TLS</dt>
            <dd>{s.secure ? 'SSL/TLS Implicit' : 'STARTTLS / Plain'}</dd>
            <dt>Username</dt>
            <dd>{s.username}</dd>
            <dt>Password</dt>
            <dd>•••••••• (Đã mã hóa trong DB)</dd>
            <dt>From Email</dt>
            <dd>{s.fromEmail}</dd>
            <dt>From Name</dt>
            <dd>{s.fromName || '—'}</dd>
            <dt>Ngày tạo</dt>
            <dd><Time value={s.createdAt} /></dd>
            <dt>Cập nhật</dt>
            <dd><Time value={s.updatedAt} /></dd>
          </dl>
        </article>

        <article className="card">
          <h2>Cập nhật thông số SMTP</h2>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              const d = new FormData(e.currentTarget);
              const payload: Record<string, unknown> = {
                host: String(d.get('host')),
                port: Number(d.get('port')),
                secure: d.get('secure') === 'true',
                username: String(d.get('username')),
                fromEmail: String(d.get('fromEmail')),
                fromName: String(d.get('fromName') || ''),
              };
              const pwd = String(d.get('password') || '');
              if (pwd.trim()) {
                payload.password = pwd;
              }
              updateMutation.mutate(payload as Partial<Sender & { password?: string }>);
            }}
          >
            <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '10px' }}>
              <label>
                SMTP Host
                <input name="host" defaultValue={s.host} required />
              </label>
              <label>
                Port
                <input name="port" type="number" defaultValue={s.port} required />
              </label>
            </div>
            <label>
              Chế độ mã hóa
              <select name="secure" defaultValue={String(s.secure)}>
                <option value="false">STARTTLS / Plain</option>
                <option value="true">SSL/TLS Implicit</option>
              </select>
            </label>
            <label>
              Username
              <input name="username" defaultValue={s.username} required />
            </label>
            <label>
              Mật khẩu mới (bỏ trống nếu giữ nguyên)
              <input name="password" type="password" placeholder="Nhập mật khẩu mới..." />
            </label>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
              <label>
                From Email
                <input name="fromEmail" type="email" defaultValue={s.fromEmail} required />
              </label>
              <label>
                From Name
                <input name="fromName" defaultValue={s.fromName || ''} />
              </label>
            </div>
            <div className="modal-actions" style={{ marginTop: '20px' }}>
              <button disabled={updateMutation.isPending}>Lưu cập nhật</button>
            </div>
          </form>
        </article>
      </div>
    </section>
  );
}
