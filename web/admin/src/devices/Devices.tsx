import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../shared/types';
import { Status, Time } from '../notifications/Status';

export type DeviceRole = 'source' | 'recipient' | 'both';

export type Device = {
  id: string;
  tenantId: string;
  userId: string;
  name: string;
  role: DeviceRole;
  status: 'active' | 'disabled';
  callbackUrl?: string;
  hasCallbackSecret: boolean;
  activeKeyCount: number;
  createdAt: string;
  updatedAt: string;
  disabledAt?: string;
};

export type DevicePage = {
  items: Device[];
  nextCursor?: string;
};

export type ApiKey = {
  id: string;
  deviceId: string;
  keyPrefix: string;
  status: string;
  createdAt: string;
};

export type CreatedApiKey = {
  id: string;
  deviceId: string;
  keyPrefix: string;
  key: string;
  status: string;
  createdAt: string;
};

export type ConfiguredCallback = {
  callbackUrl: string;
  callbackSecret: string;
};

export type PushEndpoint = {
  deviceId: string;
  platform: 'fcm' | 'apns';
  status: string;
  createdAt: string;
  updatedAt: string;
  disabledAt?: string;
  lastDeliveredAt?: string;
};

export function getRoleInfo(role: DeviceRole) {
  switch (role) {
    case 'source':
      return {
        label: 'Source (Hệ thống nguồn)',
        shortLabel: 'Source',
        icon: '📤',
        badge: '📤 Source',
        bg: '#e0f2fe',
        color: '#0369a1',
        border: '#bae6fd',
        title: 'Hệ thống gửi tin (Backend / Microservices)',
        desc: 'Được cấp API Key để đẩy tin và Webhook Callback để nhận báo cáo hoàn tất. Không nhận Push.',
      };
    case 'recipient':
      return {
        label: 'Recipient (Thiết bị nhận)',
        shortLabel: 'Recipient',
        icon: '📱',
        badge: '📱 Recipient',
        bg: '#dcfce7',
        color: '#15803d',
        border: '#bbf7d0',
        title: 'Thiết bị nhận tin (App iOS / Android)',
        desc: 'Đăng ký Push Token (FCM/APNs) để nhận thông báo nổi. Không được cấp API Key để bảo mật app.',
      };
    case 'both':
      return {
        label: 'Both (Thiết bị hai chiều)',
        shortLabel: 'Both',
        icon: '🔄',
        badge: '🔄 Both',
        bg: '#fef3c7',
        color: '#b45309',
        border: '#fde68a',
        title: 'Thiết bị hai chiều (Máy POS / App Shipper)',
        desc: 'Vừa nhận thông báo việc mới qua Push Token, vừa gọi API gửi báo cáo kết quả cho khách.',
      };
  }
}

export function DeviceList() {
  const auth = useAuth();
  const qc = useQueryClient();
  const [showCreate, setShowCreate] = useState(false);
  const [scope, setScope] = useState<'mine' | 'tenant'>('mine');
  const [status, setStatus] = useState<string>('');
  const [error, setError] = useState('');

  // Create form state
  const [formName, setFormName] = useState('');
  const [formRole, setFormRole] = useState<DeviceRole>('source');

  const q = useQuery({
    queryKey: ['devices', scope, status],
    queryFn: () => {
      const params = new URLSearchParams();
      if (scope) params.set('scope', scope);
      if (status) params.set('status', status);
      return auth.request<DevicePage>(`/v1/devices?${params.toString()}`);
    },
  });

  const create = useMutation({
    mutationFn: (data: { name: string; role: string }) =>
      auth.request<Device>('/v1/devices', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      }),
    onSuccess: () => {
      setShowCreate(false);
      setFormName('');
      setFormRole('source');
      qc.invalidateQueries({ queryKey: ['devices'] });
    },
    onError: (e) => {
      if (e instanceof ApiError) {
        setError(e.detailMessage ? `Lỗi (${e.code}): ${e.detailMessage}` : `Lỗi máy chủ: ${e.code}`);
      } else {
        setError('Không thể tạo thiết bị. Vui lòng kiểm tra lại kết nối.');
      }
    },
  });

  return (
    <section>
      <header className="page-head">
        <div>
          <div className="eyebrow">HỆ THỐNG NGUỒN & ĐÍCH</div>
          <h1>Thiết bị & Khóa API (Devices & Keys)</h1>
          <p>Quản lý định danh các hệ thống backend phát tin, ứng dụng di động nhận tin và cấp phát API Keys.</p>
        </div>
        <button
          onClick={() => {
            setError('');
            setFormName('');
            setFormRole('source');
            setShowCreate(true);
          }}
        >
          + Thêm Thiết Bị
        </button>
      </header>

      <div className="filters">
        <label>
          Phạm vi thiết bị
          <select value={scope} onChange={(e) => setScope(e.target.value as 'mine' | 'tenant')}>
            <option value="mine">Thiết bị của tôi (Mine)</option>
            <option value="tenant">Toàn bộ Tenant (Quyền Owner)</option>
          </select>
        </label>
        <label>
          Trạng thái
          <select value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">Tất cả trạng thái</option>
            <option value="active">Đang hoạt động (active)</option>
            <option value="disabled">Đã vô hiệu hóa (disabled)</option>
          </select>
        </label>
      </div>

      {showCreate && (
        <div className="modal-backdrop">
          <div className="modal" style={{ maxWidth: '620px' }}>
            <div className="modal-head">
              <div>
                <h2>Thêm thiết bị mới</h2>
                <p style={{ margin: '4px 0 0', fontSize: '0.85rem', color: 'var(--muted)' }}>
                  Đăng ký định danh thiết bị nguồn (Backend) hoặc thiết bị đích (Mobile App).
                </p>
              </div>
              <button className="ghost" onClick={() => setShowCreate(false)}>✕</button>
            </div>

            <form
              onSubmit={(e) => {
                e.preventDefault();
                create.mutate({
                  name: formName.trim(),
                  role: formRole,
                });
              }}
            >
              <label>
                Tên thiết bị / Hệ thống
                <input
                  value={formName}
                  onChange={(e) => setFormName(e.target.value)}
                  placeholder="vd: Order Backend Service, iPhone 15 của Hưởng, Máy POS Bán Hàng..."
                  required
                  maxLength={100}
                />
              </label>

              <div>
                <div style={{ fontSize: '0.8rem', fontWeight: 700, color: 'var(--muted)', marginBottom: '8px' }}>
                  CHỌN VAI TRÒ THIẾT BỊ (ROLE):
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))', gap: '8px' }}>
                  {(['source', 'recipient', 'both'] as const).map((r) => {
                    const info = getRoleInfo(r);
                    const active = formRole === r;
                    return (
                      <button
                        key={r}
                        type="button"
                        className="ghost"
                        onClick={() => setFormRole(r)}
                        style={{
                          padding: '10px 12px',
                          fontSize: '0.85rem',
                          textAlign: 'left',
                          display: 'flex',
                          flexDirection: 'column',
                          gap: '4px',
                          borderColor: active ? 'var(--ink)' : 'var(--line)',
                          background: active ? '#17221e' : 'white',
                          color: active ? 'white' : 'inherit',
                        }}
                      >
                        <div style={{ fontWeight: 700, display: 'flex', alignItems: 'center', gap: '6px' }}>
                          <span>{info.icon}</span> {info.shortLabel}
                        </div>
                        <div style={{ fontSize: '0.72rem', opacity: active ? 0.85 : 0.65, lineHeight: 1.3 }}>
                          {info.title}
                        </div>
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Role explanation callout */}
              <div
                style={{
                  padding: '10px 14px',
                  background: getRoleInfo(formRole).bg,
                  border: `1px solid ${getRoleInfo(formRole).border}`,
                  borderRadius: '8px',
                  fontSize: '0.84rem',
                  color: getRoleInfo(formRole).color,
                  lineHeight: 1.45,
                }}
              >
                <strong>{getRoleInfo(formRole).label}:</strong> {getRoleInfo(formRole).desc}
              </div>

              {error && <div className="error" role="alert">{error}</div>}

              <div className="modal-actions">
                <button type="button" className="ghost" onClick={() => setShowCreate(false)}>Hủy</button>
                <button disabled={create.isPending || !formName.trim()}>
                  {create.isPending ? 'Đang tạo…' : 'Tạo Thiết Bị'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {q.isLoading ? (
        <p>Đang tải danh sách thiết bị…</p>
      ) : q.error ? (
        <div className="error">Không tải được danh sách thiết bị. Vui lòng kiểm tra quyền truy cập.</div>
      ) : q.data?.items.length === 0 ? (
        <div className="empty">
          <h2>Chưa có thiết bị nào</h2>
          <p>Tạo thiết bị đầu tiên để cấp API key gửi thông báo hoặc liên kết push token di động.</p>
          <button
            style={{ marginTop: '16px' }}
            onClick={() => {
              setError('');
              setShowCreate(true);
            }}
          >
            + Thêm Thiết Bị Đầu Tiên
          </button>
        </div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Tên Thiết Bị</th>
                <th>Vai Trò</th>
                <th>Trạng Thái</th>
                <th>Khóa API (Active)</th>
                <th>Webhook Callback</th>
                <th>Ngày Tạo</th>
                <th style={{ textAlign: 'right' }}>Thao Tác</th>
              </tr>
            </thead>
            <tbody>
              {q.data?.items.map((d) => {
                const roleInfo = getRoleInfo(d.role);
                return (
                  <tr key={d.id}>
                    <td>
                      <Link
                        to={`/devices/${d.id}`}
                        className="id"
                        style={{ fontWeight: 700, color: 'var(--primary, #0ea5e9)', textDecoration: 'none' }}
                      >
                        {d.name}
                      </Link>
                      <small style={{ fontFamily: 'monospace' }}>{d.id}</small>
                    </td>
                    <td>
                      <span
                        className="badge"
                        style={{
                          background: roleInfo.bg,
                          color: roleInfo.color,
                          border: `1px solid ${roleInfo.border}`,
                          fontSize: '0.75rem',
                          padding: '2px 8px',
                        }}
                      >
                        {roleInfo.badge}
                      </span>
                    </td>
                    <td>
                      <Status value={d.status} />
                    </td>
                    <td>
                      {d.role === 'recipient' ? (
                        <span style={{ color: 'var(--muted)', fontSize: '0.85rem' }}>— (Không dùng Key)</span>
                      ) : (
                        <span style={{ fontWeight: 600 }}>{d.activeKeyCount ?? 0} keys</span>
                      )}
                    </td>
                    <td>
                      {d.callbackUrl ? (
                        <span
                          style={{
                            color: '#0369a1',
                            background: '#f0f9ff',
                            padding: '2px 6px',
                            borderRadius: '4px',
                            fontSize: '0.8rem',
                          }}
                          title={d.callbackUrl}
                        >
                          ✓ {new URL(d.callbackUrl).host}
                        </span>
                      ) : (
                        <span style={{ color: 'var(--muted)' }}>— Chưa cấu hình</span>
                      )}
                    </td>
                    <td><Time value={d.createdAt} /></td>
                    <td style={{ textAlign: 'right' }}>
                      <Link
                        to={`/devices/${d.id}`}
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
                        ⚙️ Chi tiết / Quản lý Key
                      </Link>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

export function DeviceDetail() {
  const { id } = useParams();
  const auth = useAuth();
  const nav = useNavigate();
  const qc = useQueryClient();

  const [renameText, setRenameText] = useState('');
  const [isRenaming, setIsRenaming] = useState(false);
  const [callbackUrlInput, setCallbackUrlInput] = useState('');
  const [showCallbackModal, setShowCallbackModal] = useState(false);
  const [newCallbackResult, setNewCallbackResult] = useState<ConfiguredCallback | null>(null);
  const [newCreatedKey, setNewCreatedKey] = useState<CreatedApiKey | null>(null);
  const [showPushModal, setShowPushModal] = useState(false);
  const [pushPlatform, setPushPlatform] = useState<'fcm' | 'apns'>('fcm');
  const [pushTokenInput, setPushTokenInput] = useState('');
  const [actionError, setActionError] = useState('');
  const [copyNotice, setCopyNotice] = useState('');
  const [successNotice, setSuccessNotice] = useState('');

  const deviceQuery = useQuery({
    queryKey: ['device', id],
    queryFn: () => auth.request<Device>(`/v1/devices/${id}`),
  });

  const keysQuery = useQuery({
    queryKey: ['device-keys', id],
    queryFn: () => auth.request<{ items: ApiKey[] }>(`/v1/devices/${id}/api-keys`),
  });

  const pushQuery = useQuery({
    queryKey: ['device-push', id],
    queryFn: async () => {
      try {
        return await auth.request<PushEndpoint>(`/v1/devices/${id}/push-endpoint`);
      } catch {
        return null;
      }
    },
  });

  const renameMutation = useMutation({
    mutationFn: (name: string) =>
      auth.request<Device>(`/v1/devices/${id}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name }),
      }),
    onSuccess: () => {
      setIsRenaming(false);
      setSuccessNotice('Đã đổi tên thiết bị thành công!');
      setTimeout(() => setSuccessNotice(''), 3000);
      deviceQuery.refetch();
      qc.invalidateQueries({ queryKey: ['devices'] });
    },
    onError: (e) => {
      setActionError(e instanceof ApiError ? (e.detailMessage || e.code) : 'Đổi tên thất bại.');
    },
  });

  const disableMutation = useMutation({
    mutationFn: () =>
      auth.request<void>(`/v1/devices/${id}/disable`, {
        method: 'POST',
      }),
    onSuccess: () => {
      setSuccessNotice('Đã vô hiệu hóa thiết bị và toàn bộ API Key liên quan.');
      setTimeout(() => setSuccessNotice(''), 4000);
      deviceQuery.refetch();
      keysQuery.refetch();
      qc.invalidateQueries({ queryKey: ['devices'] });
    },
    onError: (e) => {
      setActionError(e instanceof ApiError ? (e.detailMessage || e.code) : 'Không thể vô hiệu hóa thiết bị.');
    },
  });

  const createKeyMutation = useMutation({
    mutationFn: () =>
      auth.request<CreatedApiKey>(`/v1/devices/${id}/api-keys`, {
        method: 'POST',
      }),
    onSuccess: (data) => {
      setNewCreatedKey(data);
      keysQuery.refetch();
      deviceQuery.refetch();
      qc.invalidateQueries({ queryKey: ['devices'] });
    },
    onError: (e) => {
      if (e instanceof ApiError && e.code === 'DEVICE_API_KEY_LIMIT_REACHED') {
        setActionError('Đã đạt giới hạn tối đa số lượng API Key cho thiết bị này (10 keys). Vui lòng thu hồi bớt key cũ.');
      } else if (e instanceof ApiError) {
        setActionError(e.detailMessage || `Lỗi tạo key: ${e.code}`);
      } else {
        setActionError('Không thể tạo API key mới.');
      }
    },
  });

  const revokeKeyMutation = useMutation({
    mutationFn: (keyId: string) =>
      auth.request<void>(`/v1/devices/${id}/api-keys/${keyId}`, {
        method: 'DELETE',
      }),
    onSuccess: () => {
      setSuccessNotice('Đã thu hồi API Key thành công. Khóa đã bị vô hiệu hóa.');
      setTimeout(() => setSuccessNotice(''), 3000);
      keysQuery.refetch();
      deviceQuery.refetch();
      qc.invalidateQueries({ queryKey: ['devices'] });
    },
    onError: (e) => {
      setActionError(e instanceof ApiError ? (e.detailMessage || e.code) : 'Không thể thu hồi API key.');
    },
  });

  const configCallbackMutation = useMutation({
    mutationFn: (url: string) =>
      auth.request<ConfiguredCallback>(`/v1/devices/${id}/callback`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ url }),
      }),
    onSuccess: (data) => {
      setNewCallbackResult(data);
      deviceQuery.refetch();
    },
    onError: (e) => {
      setActionError(e instanceof ApiError ? (e.detailMessage || e.code) : 'Cấu hình callback thất bại.');
    },
  });

  const clearCallbackMutation = useMutation({
    mutationFn: () =>
      auth.request<void>(`/v1/devices/${id}/callback`, {
        method: 'DELETE',
      }),
    onSuccess: () => {
      setSuccessNotice('Đã xóa cấu hình Webhook Callback.');
      setTimeout(() => setSuccessNotice(''), 3000);
      deviceQuery.refetch();
    },
    onError: (e) => {
      setActionError(e instanceof ApiError ? (e.detailMessage || e.code) : 'Xóa callback thất bại.');
    },
  });

  const registerPushMutation = useMutation({
    mutationFn: (data: { platform: string; token: string }) =>
      auth.request<PushEndpoint>(`/v1/devices/${id}/push-endpoint`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      }),
    onSuccess: () => {
      setShowPushModal(false);
      setPushTokenInput('');
      setSuccessNotice('Đã đăng ký Push Token thành công! Token đã được mã hóa AES-256-GCM.');
      setTimeout(() => setSuccessNotice(''), 3500);
      pushQuery.refetch();
    },
    onError: (e) => {
      setActionError(e instanceof ApiError ? (e.detailMessage || e.code) : 'Đăng ký push token thất bại.');
    },
  });

  const revokePushMutation = useMutation({
    mutationFn: () =>
      auth.request<void>(`/v1/devices/${id}/push-endpoint`, {
        method: 'DELETE',
      }),
    onSuccess: () => {
      setSuccessNotice('Đã hủy và vô hiệu hóa Push Token của thiết bị.');
      setTimeout(() => setSuccessNotice(''), 3000);
      pushQuery.refetch();
    },
    onError: (e) => {
      setActionError(e instanceof ApiError ? (e.detailMessage || e.code) : 'Hủy push token thất bại.');
    },
  });

  const copyToClipboard = (text: string, label: string) => {
    navigator.clipboard.writeText(text);
    setCopyNotice(`Đã sao chép ${label}!`);
    setTimeout(() => setCopyNotice(''), 3000);
  };

  if (deviceQuery.isLoading) return <p>Đang tải thông tin thiết bị…</p>;
  if (deviceQuery.error || !deviceQuery.data) return <div className="error">Không tìm thấy thông tin thiết bị.</div>;

  const d = deviceQuery.data;
  const push = pushQuery.data;
  const roleInfo = getRoleInfo(d.role);

  return (
    <section>
      <button className="back ghost" onClick={() => nav('/devices')}>← Quay lại danh sách thiết bị</button>

      <header className="page-head">
        <div>
          <div className="eyebrow">CHI TIẾT THIẾT BỊ</div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <h1>{d.name}</h1>
            <span
              className="badge"
              style={{
                background: roleInfo.bg,
                color: roleInfo.color,
                border: `1px solid ${roleInfo.border}`,
                fontSize: '0.8rem',
                padding: '3px 10px',
              }}
            >
              {roleInfo.badge}
            </span>
            <Status value={d.status} />
          </div>
          <p>ID Thiết Bị: <code>{d.id}</code></p>
        </div>

        <div className="actions">
          {d.status === 'active' && (
            <>
              <button className="ghost" onClick={() => { setRenameText(d.name); setIsRenaming(true); }}>
                ✏️ Đổi tên
              </button>
              <button
                className="danger"
                disabled={disableMutation.isPending}
                onClick={() => {
                  if (confirm(`Vô hiệu hóa thiết bị "${d.name}"? Toàn bộ API Key trực thuộc sẽ lập tức ngừng hoạt động vĩnh viễn!`)) {
                    disableMutation.mutate();
                  }
                }}
              >
                Vô hiệu hóa thiết bị
              </button>
            </>
          )}
        </div>
      </header>

      {/* Role Architecture & Security Guidance Banner */}
      <div
        style={{
          padding: '14px 18px',
          background: roleInfo.bg,
          border: `1px solid ${roleInfo.border}`,
          borderRadius: '10px',
          color: roleInfo.color,
          marginBottom: '22px',
          fontSize: '0.88rem',
          lineHeight: 1.5,
          display: 'flex',
          alignItems: 'flex-start',
          gap: '12px',
        }}
      >
        <span style={{ fontSize: '1.4rem' }}>{roleInfo.icon}</span>
        <div>
          <strong>Kiến trúc & Vai trò: {roleInfo.label}</strong>
          <p style={{ margin: '4px 0 0' }}>{roleInfo.desc}</p>
        </div>
      </div>

      {copyNotice && <div className="success">{copyNotice}</div>}
      {successNotice && <div className="success">{successNotice}</div>}
      {actionError && <div className="error">{actionError}</div>}

      {/* Rename Modal */}
      {isRenaming && (
        <div className="modal-backdrop">
          <div className="modal" style={{ maxWidth: '480px' }}>
            <div className="modal-head">
              <h2>Đổi tên thiết bị</h2>
              <button className="ghost" onClick={() => setIsRenaming(false)}>✕</button>
            </div>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                renameMutation.mutate(renameText.trim());
              }}
            >
              <label>
                Tên mới của thiết bị
                <input
                  value={renameText}
                  onChange={(e) => setRenameText(e.target.value)}
                  required
                  maxLength={100}
                />
              </label>
              <div className="modal-actions">
                <button type="button" className="ghost" onClick={() => setIsRenaming(false)}>Hủy</button>
                <button disabled={renameMutation.isPending || !renameText.trim()}>
                  {renameMutation.isPending ? 'Đang lưu…' : 'Lưu Thay Đổi'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* New API Key Created Modal */}
      {newCreatedKey && (
        <div className="modal-backdrop">
          <div className="modal" style={{ maxWidth: '640px' }}>
            <div className="modal-head">
              <div>
                <h2>Đã tạo API Key mới thành công</h2>
                <p style={{ margin: '4px 0 0', fontSize: '0.85rem', color: 'var(--muted)' }}>
                  Khóa dùng để xác thực các request gửi thông báo vào hệ thống.
                </p>
              </div>
              <button className="ghost" onClick={() => setNewCreatedKey(null)}>✕</button>
            </div>

            <div
              style={{
                padding: '10px 14px',
                background: '#fffbeb',
                border: '1px solid #fde68a',
                borderRadius: '8px',
                color: '#92400e',
                fontSize: '0.85rem',
                lineHeight: 1.45,
                marginBottom: '14px',
              }}
            >
              ⚠️ <strong>Cảnh báo quan trọng:</strong> Đây là <strong>LẦN DUY NHẤT</strong> mã API Key này hiển thị đầy đủ. Sau khi đóng popup, chuỗi khóa bí mật sẽ được băm một chiều (Hash SHA-256) và không thể xem lại!
            </div>

            <div className="key-box" style={{ flexDirection: 'column', alignItems: 'stretch', gap: '8px' }}>
              <div style={{ fontSize: '0.75rem', color: '#94a3b8' }}>RAW API KEY (BEARER TOKEN):</div>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ fontSize: '0.9rem', wordBreak: 'break-all', color: '#38bdf8' }}>
                  {newCreatedKey.key}
                </span>
                <button
                  type="button"
                  onClick={() => copyToClipboard(newCreatedKey.key, 'API Key')}
                  style={{ background: '#0284c7' }}
                >
                  Sao chép
                </button>
              </div>
            </div>

            {/* Test snippet with curl */}
            <div style={{ marginTop: '16px' }}>
              <div style={{ fontSize: '0.78rem', fontWeight: 700, color: 'var(--muted)', marginBottom: '6px' }}>
                LỆNH CURL KIỂM THỬ GỬI TIN NHANH VỚI KEY NÀY:
              </div>
              <pre
                style={{
                  background: '#0f172a',
                  color: '#e2e8f0',
                  padding: '12px',
                  borderRadius: '8px',
                  fontSize: '0.78rem',
                  overflowX: 'auto',
                  lineHeight: 1.4,
                  margin: 0,
                }}
              >
{`curl -X POST "https://notification-len1.onrender.com/v1/notifications" \\
  -H "Content-Type: application/json" \\
  -H "X-API-Key: ${newCreatedKey.key}" \\
  -d '{"recipientEmail":"huong102145@st.vimaru.edu.vn","subject":"Test API Key","body":"Hello from Device Key"}'`}
              </pre>
            </div>

            <div className="modal-actions" style={{ marginTop: '20px' }}>
              <button onClick={() => setNewCreatedKey(null)}>Tôi Đã Lưu Khóa Xong</button>
            </div>
          </div>
        </div>
      )}

      {/* Push Token Config Modal with Mock Presets */}
      {showPushModal && (
        <div className="modal-backdrop">
          <div className="modal" style={{ maxWidth: '580px' }}>
            <div className="modal-head">
              <div>
                <h2>Đăng ký Push Token cho Mobile App</h2>
                <p style={{ margin: '4px 0 0', fontSize: '0.85rem', color: 'var(--muted)' }}>
                  Lưu trữ và mã hóa bảo mật chuẩn AES-256-GCM.
                </p>
              </div>
              <button className="ghost" onClick={() => setShowPushModal(false)}>✕</button>
            </div>

            {/* Mock Test Buttons */}
            <div
              style={{
                padding: '10px 14px',
                background: '#f8faf9',
                border: '1px solid var(--line)',
                borderRadius: '8px',
                marginBottom: '14px',
              }}
            >
              <div style={{ fontSize: '0.78rem', fontWeight: 700, color: 'var(--muted)', marginBottom: '6px' }}>
                KIỂM THỬ NHANH (MOCK TOKENS):
              </div>
              <div style={{ display: 'flex', gap: '8px' }}>
                <button
                  type="button"
                  className="ghost"
                  style={{ fontSize: '0.78rem', padding: '4px 10px' }}
                  onClick={() => {
                    setPushPlatform('fcm');
                    setPushTokenInput('fcm_mock_token_test_123456789_abcdef_demo');
                  }}
                >
                  ⚡ Điền Mock Token FCM (Android)
                </button>
                <button
                  type="button"
                  className="ghost"
                  style={{ fontSize: '0.78rem', padding: '4px 10px' }}
                  onClick={() => {
                    setPushPlatform('apns');
                    setPushTokenInput('apns_mock_token_test_987654321_fedcba_demo');
                  }}
                >
                  ⚡ Điền Mock Token APNs (iOS)
                </button>
              </div>
            </div>

            <form
              onSubmit={(e) => {
                e.preventDefault();
                registerPushMutation.mutate({
                  platform: pushPlatform,
                  token: pushTokenInput.trim(),
                });
              }}
            >
              <label>
                Nền tảng di động
                <select
                  value={pushPlatform}
                  onChange={(e) => setPushPlatform(e.target.value as 'fcm' | 'apns')}
                >
                  <option value="fcm">Android / Google Firebase (FCM)</option>
                  <option value="apns">iOS / Apple Push (APNs)</option>
                </select>
              </label>

              <label>
                Push Device Token
                <textarea
                  rows={3}
                  placeholder="fcm_token_xyz... hoặc apns_token_..."
                  value={pushTokenInput}
                  onChange={(e) => setPushTokenInput(e.target.value)}
                  required
                />
              </label>

              <div className="modal-actions">
                <button type="button" className="ghost" onClick={() => setShowPushModal(false)}>Hủy</button>
                <button disabled={registerPushMutation.isPending || !pushTokenInput.trim()}>
                  {registerPushMutation.isPending ? 'Đang lưu…' : 'Lưu Token Mã Hóa'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Callback Config Modal with webhook.site guidance */}
      {showCallbackModal && (
        <div className="modal-backdrop">
          <div className="modal" style={{ maxWidth: '600px' }}>
            <div className="modal-head">
              <div>
                <h2>Cấu hình Webhook Callback (HMAC)</h2>
                <p style={{ margin: '4px 0 0', fontSize: '0.85rem', color: 'var(--muted)' }}>
                  Nhận báo cáo sự kiện <code>notification.completed</code> có đính kèm chữ ký bảo mật.
                </p>
              </div>
              <button
                className="ghost"
                onClick={() => {
                  setShowCallbackModal(false);
                  setNewCallbackResult(null);
                }}
              >
                ✕
              </button>
            </div>

            {!newCallbackResult ? (
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  configCallbackMutation.mutate(callbackUrlInput.trim());
                }}
              >
                {/* Webhook.site helper callout */}
                <div
                  style={{
                    padding: '12px 14px',
                    background: '#f0f9ff',
                    border: '1px solid #bae6fd',
                    borderRadius: '8px',
                    fontSize: '0.84rem',
                    color: '#0369a1',
                    lineHeight: 1.45,
                  }}
                >
                  <strong>💡 Cách kiểm thử Webhook ngay lập tức:</strong>
                  <p style={{ margin: '4px 0 6px' }}>
                    1. Mở trang <a href="https://webhook.site" target="_blank" rel="noreferrer" style={{ fontWeight: 700, color: '#0284c7' }}>webhook.site</a> để lấy URL hứng request miễn phí.
                  </p>
                  <p style={{ margin: 0 }}>
                    2. Dán URL tạm thời đó vào ô bên dưới rồi bấm Lưu để nhận mã bí mật HMAC Secret.
                  </p>
                </div>

                <label>
                  Webhook Callback URL
                  <input
                    type="url"
                    placeholder="https://webhook.site/08c34f9a-xxxx-xxxx... hoặc https://your-domain.com/webhook"
                    value={callbackUrlInput}
                    onChange={(e) => setCallbackUrlInput(e.target.value)}
                    required
                  />
                </label>

                <div className="modal-actions">
                  <button
                    type="button"
                    className="ghost"
                    onClick={() => {
                      setShowCallbackModal(false);
                      setNewCallbackResult(null);
                    }}
                  >
                    Hủy
                  </button>
                  <button disabled={configCallbackMutation.isPending || !callbackUrlInput.trim()}>
                    {configCallbackMutation.isPending ? 'Đang lưu…' : 'Lưu URL & Sinh Secret HMAC'}
                  </button>
                </div>
              </form>
            ) : (
              <div>
                <div className="success">Đã lưu cấu hình Webhook Callback thành công!</div>

                <div
                  style={{
                    padding: '10px 14px',
                    background: '#fffbeb',
                    border: '1px solid #fde68a',
                    borderRadius: '8px',
                    color: '#92400e',
                    fontSize: '0.85rem',
                    lineHeight: 1.45,
                    margin: '12px 0',
                  }}
                >
                  ⚠️ <strong>HMAC Secret (Khóa bí mật ký số):</strong>
                  <p style={{ margin: '4px 0 0' }}>
                    Server của bạn dùng mã này để xác thực chữ ký số <code>X-Signature-SHA256</code> đính kèm trong mỗi gói tin callback.
                  </p>
                </div>

                <div className="key-box">
                  <span style={{ color: '#38bdf8' }}>{newCallbackResult.callbackSecret}</span>
                  <button
                    type="button"
                    onClick={() => copyToClipboard(newCallbackResult.callbackSecret, 'HMAC Secret')}
                  >
                    Sao chép
                  </button>
                </div>

                <div className="modal-actions" style={{ marginTop: '20px' }}>
                  <button
                    onClick={() => {
                      setShowCallbackModal(false);
                      setNewCallbackResult(null);
                    }}
                  >
                    Đóng
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Grid of 3 Architectural Cards */}
      <div className="grid">
        {/* Card 1: Thông tin chung */}
        <article className="card">
          <h2>Thông tin thiết bị</h2>
          <dl>
            <dt>Vai trò</dt>
            <dd>
              <span
                className="badge"
                style={{
                  background: roleInfo.bg,
                  color: roleInfo.color,
                  border: `1px solid ${roleInfo.border}`,
                }}
              >
                {roleInfo.badge}
              </span>
            </dd>

            <dt>Trạng thái</dt>
            <dd><Status value={d.status} /></dd>

            <dt>User sở hữu</dt>
            <dd><code style={{ fontSize: '0.85rem' }}>{d.userId}</code></dd>

            <dt>Ngày tạo</dt>
            <dd><Time value={d.createdAt} /></dd>

            <dt>Cập nhật</dt>
            <dd><Time value={d.updatedAt} /></dd>

            {d.disabledAt && (
              <>
                <dt>Vô hiệu hóa lúc</dt>
                <dd style={{ color: 'var(--danger)' }}><Time value={d.disabledAt} /></dd>
              </>
            )}
          </dl>
        </article>

        {/* Card 2: Webhook Callback (HMAC) */}
        <article className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
            <h2 style={{ margin: 0 }}>Webhook Callback (HMAC)</h2>
            <a
              href="https://webhook.site"
              target="_blank"
              rel="noreferrer"
              style={{ fontSize: '0.75rem', color: 'var(--primary, #0ea5e9)', textDecoration: 'none' }}
            >
              🔗 Dùng webhook.site để test
            </a>
          </div>

          {d.callbackUrl ? (
            <div>
              <dl>
                <dt>Callback URL</dt>
                <dd><code style={{ wordBreak: 'break-all' }}>{d.callbackUrl}</code></dd>

                <dt>Chữ ký HMAC</dt>
                <dd>
                  <span style={{ color: '#16a34a', fontWeight: 600 }}>
                    ✓ Đã kích hoạt bảo mật (X-Signature-SHA256)
                  </span>
                </dd>
              </dl>
              <div className="actions" style={{ marginTop: '18px' }}>
                <button
                  className="ghost"
                  onClick={() => {
                    setCallbackUrlInput(d.callbackUrl || '');
                    setShowCallbackModal(true);
                  }}
                >
                  Cập nhật URL
                </button>
                <button
                  className="ghost danger"
                  disabled={clearCallbackMutation.isPending}
                  onClick={() => {
                    if (confirm('Bạn có chắc chắn muốn xóa cấu hình Webhook Callback này?')) {
                      clearCallbackMutation.mutate();
                    }
                  }}
                >
                  Xóa Callback
                </button>
              </div>
            </div>
          ) : (
            <div>
              <p style={{ fontSize: '0.88rem', color: 'var(--muted)', margin: '0 0 14px' }}>
                Chưa cấu hình callback. Khi có callback URL, background worker sẽ tự động bắn thông báo hoàn tất <code>notification.completed</code> về máy chủ của bạn.
              </p>
              <button
                className="ghost"
                onClick={() => {
                  setCallbackUrlInput('');
                  setShowCallbackModal(true);
                }}
              >
                + Cấu hình Webhook Callback
              </button>
            </div>
          )}
        </article>

        {/* Card 3: Mobile Push Endpoint (FCM / APNs) */}
        <article className="card" style={{ gridColumn: '1 / -1' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
            <h2 style={{ margin: 0 }}>Mobile Push Endpoint (Google FCM / Apple APNs)</h2>
            {d.role === 'source' && (
              <span className="badge badge-muted">Thiết bị Source: Không khuyến nghị nhận Push</span>
            )}
          </div>

          {push ? (
            <div>
              <dl style={{ gridTemplateColumns: '180px 1fr' }}>
                <dt>Nền tảng di động</dt>
                <dd>
                  <strong>{push.platform.toUpperCase()}</strong> ({push.platform === 'fcm' ? 'Android / Google Firebase' : 'iOS / Apple Push'})
                </dd>

                <dt>Trạng thái Push</dt>
                <dd><Status value={push.status} /></dd>

                <dt>Bảo mật Token</dt>
                <dd style={{ color: 'var(--muted)' }}>•••••••• (Đã mã hóa AES-256-GCM trong CSDL)</dd>

                <dt>Đăng ký lúc</dt>
                <dd><Time value={push.createdAt} /></dd>

                {push.lastDeliveredAt && (
                  <>
                    <dt>Lần đẩy tin gần nhất</dt>
                    <dd style={{ color: '#16a34a' }}><Time value={push.lastDeliveredAt} /></dd>
                  </>
                )}
              </dl>
              <div className="actions" style={{ marginTop: '18px' }}>
                <button className="ghost" onClick={() => setShowPushModal(true)}>Cập nhật Token Mới</button>
                {push.status === 'active' && (
                  <button
                    className="ghost danger"
                    disabled={revokePushMutation.isPending}
                    onClick={() => {
                      if (confirm('Vô hiệu hóa và hủy Push Token của thiết bị này?')) {
                        revokePushMutation.mutate();
                      }
                    }}
                  >
                    Hủy Push Token
                  </button>
                )}
              </div>
            </div>
          ) : (
            <div>
              {d.role === 'source' ? (
                <div style={{ color: 'var(--muted)', fontSize: '0.88rem' }}>
                  Hệ thống này đang có vai trò <strong>Source (Hệ thống nguồn)</strong>. Theo kiến trúc phân quyền tối thiểu (Least Privilege), hệ thống nguồn không nhận push notification.
                </div>
              ) : (
                <p style={{ fontSize: '0.88rem', color: 'var(--muted)', margin: '0 0 14px' }}>
                  Chưa đăng ký Push Token. Thiết bị di động có thể gửi chuỗi token được cấp bởi Firebase (FCM) hoặc Apple (APNs) lên để nhận thông báo đẩy trực tiếp.
                </p>
              )}
              <button
                className="ghost"
                style={{ marginTop: '8px' }}
                onClick={() => setShowPushModal(true)}
              >
                + Đăng ký Push Token (FCM / APNs)
              </button>
            </div>
          )}
        </article>
      </div>

      {/* Section 4: API Keys Management */}
      <div style={{ marginTop: '32px' }}>
        <header className="page-head">
          <div>
            <h2>Danh sách Khóa API (API Keys)</h2>
            <p>Các key dùng để xác thực request gửi thông báo (<code>POST /v1/notifications</code>) từ thiết bị này.</p>
          </div>

          {d.status === 'active' && d.role !== 'recipient' && (
            <button
              disabled={createKeyMutation.isPending}
              onClick={() => createKeyMutation.mutate()}
            >
              + Tạo API Key Mới
            </button>
          )}
        </header>

        {/* Security Warning if role is recipient */}
        {d.role === 'recipient' && (
          <div
            style={{
              padding: '12px 16px',
              background: '#fffbeb',
              border: '1px solid #fde68a',
              borderRadius: '8px',
              color: '#92400e',
              fontSize: '0.85rem',
              lineHeight: 1.45,
              marginBottom: '16px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: '12px',
            }}
          >
            <div>
              🛡️ <strong>Khuyến nghị an toàn:</strong> Thiết bị này có vai trò <strong>Recipient (App di động)</strong>. Tuyệt đối KHÔNG cấp API Key cho app di động để phòng ngừa nguy cơ bị dịch ngược (decompile APK/IPA) và đánh cắp quyền gửi tin.
            </div>
            {d.status === 'active' && (
              <button
                type="button"
                className="ghost"
                style={{ fontSize: '0.78rem', flexShrink: 0, background: 'white' }}
                onClick={() => {
                  if (confirm('CẢNH BÁO AN NINH: Bạn đang tạo API Key cho một thiết bị Recipient. Bạn có chắc chắn hiểu rõ rủi ro bảo mật?')) {
                    createKeyMutation.mutate();
                  }
                }}
              >
                Vẫn muốn tạo Key
              </button>
            )}
          </div>
        )}

        {keysQuery.isLoading ? (
          <p>Đang tải danh sách khóa API…</p>
        ) : keysQuery.data?.items.length === 0 ? (
          <div className="empty">
            <p>Chưa có API Key nào được cấp phát cho thiết bị này.</p>
            {d.role !== 'recipient' && d.status === 'active' && (
              <button
                style={{ marginTop: '8px' }}
                disabled={createKeyMutation.isPending}
                onClick={() => createKeyMutation.mutate()}
              >
                + Cấp API Key Đầu Tiên
              </button>
            )}
          </div>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Prefix Khóa</th>
                  <th>Trạng Thái</th>
                  <th>Ngày Tạo</th>
                  <th style={{ textAlign: 'right' }}>Hành Động</th>
                </tr>
              </thead>
              <tbody>
                {keysQuery.data?.items.map((k) => (
                  <tr key={k.id}>
                    <td>
                      <code style={{ fontSize: '0.9rem', color: '#0369a1', fontWeight: 600 }}>
                        {k.keyPrefix}••••••••
                      </code>
                    </td>
                    <td>
                      <Status value={k.status} />
                    </td>
                    <td><Time value={k.createdAt} /></td>
                    <td style={{ textAlign: 'right' }}>
                      {k.status === 'active' && (
                        <button
                          className="ghost danger"
                          style={{ padding: '4px 10px', fontSize: '13px' }}
                          disabled={revokeKeyMutation.isPending}
                          onClick={() => {
                            if (confirm(`Thu hồi API key [${k.keyPrefix}••••]? Khóa này sẽ lập tức mất quyền gửi tin vĩnh viễn.`)) {
                              revokeKeyMutation.mutate(k.id);
                            }
                          }}
                        >
                          Thu hồi (Revoke)
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </section>
  );
}
