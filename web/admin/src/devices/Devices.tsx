import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../shared/types';
import { Status, Time } from '../notifications/Status';

type Device = {
  id: string;
  tenantId: string;
  userId: string;
  name: string;
  role: 'source' | 'recipient' | 'both';
  status: 'active' | 'disabled';
  callbackUrl?: string;
  hasCallbackSecret: boolean;
  activeKeyCount: number;
  createdAt: string;
  updatedAt: string;
  disabledAt?: string;
};

type DevicePage = {
  items: Device[];
  nextCursor?: string;
};

type ApiKey = {
  id: string;
  deviceId: string;
  keyPrefix: string;
  status: string;
  createdAt: string;
};

type CreatedApiKey = {
  id: string;
  deviceId: string;
  keyPrefix: string;
  key: string;
  status: string;
  createdAt: string;
};

type ConfiguredCallback = {
  callbackUrl: string;
  callbackSecret: string;
};

type PushEndpoint = {
  deviceId: string;
  platform: 'fcm' | 'apns';
  status: string;
  createdAt: string;
  updatedAt: string;
  disabledAt?: string;
  lastDeliveredAt?: string;
};

export function DeviceList() {
  const auth = useAuth();
  const qc = useQueryClient();
  const [showCreate, setShowCreate] = useState(false);
  const [scope, setScope] = useState<'mine' | 'tenant'>('mine');
  const [status, setStatus] = useState<string>('');
  const [error, setError] = useState('');

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
      qc.invalidateQueries({ queryKey: ['devices'] });
    },
    onError: (e) => {
      setError(e instanceof ApiError ? `Lỗi: ${e.code}` : 'Không thể tạo thiết bị.');
    },
  });

  return (
    <section>
      <header className="page-head">
        <div>
          <div className="eyebrow">HỆ THỐNG NGUỒN & ĐÍCH</div>
          <h1>Thiết bị & API Keys</h1>
          <p>Quản lý các source/recipient devices và API keys gửi nhận thông báo.</p>
        </div>
        <button onClick={() => { setError(''); setShowCreate(true); }}>Thêm thiết bị</button>
      </header>

      <div className="filters">
        <label>
          Phạm vi
          <select value={scope} onChange={(e) => setScope(e.target.value as 'mine' | 'tenant')}>
            <option value="mine">Của tôi</option>
            <option value="tenant">Toàn bộ Tenant (Owner)</option>
          </select>
        </label>
        <label>
          Trạng thái
          <select value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">Tất cả</option>
            <option value="active">Đang hoạt động</option>
            <option value="disabled">Đã vô hiệu hóa</option>
          </select>
        </label>
      </div>

      {showCreate && (
        <div className="modal-backdrop">
          <div className="modal">
            <div className="modal-head">
              <h2>Thêm thiết bị mới</h2>
              <button className="ghost" onClick={() => setShowCreate(false)}>✕</button>
            </div>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                const d = new FormData(e.currentTarget);
                create.mutate({
                  name: String(d.get('name')),
                  role: String(d.get('role')),
                });
              }}
            >
              <label>
                Tên thiết bị (hệ thống)
                <input name="name" placeholder="vd: Web Backend, Mobile App..." required maxLength={100} />
              </label>
              <label>
                Vai trò
                <select name="role" defaultValue="source">
                  <option value="source">Source (Hệ thống gửi)</option>
                  <option value="recipient">Recipient (Thiết bị nhận)</option>
                  <option value="both">Both (Cả hai)</option>
                </select>
              </label>
              {error && <div className="error" role="alert">{error}</div>}
              <div className="modal-actions">
                <button type="button" className="ghost" onClick={() => setShowCreate(false)}>Hủy</button>
                <button disabled={create.isPending}>Tạo thiết bị</button>
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
          <p>Tạo thiết bị đầu tiên để cấp API key gửi thông báo.</p>
        </div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Thiết bị</th>
                <th>Vai trò</th>
                <th>Trạng thái</th>
                <th>API Keys Active</th>
                <th>Webhook Callback</th>
                <th>Ngày tạo</th>
              </tr>
            </thead>
            <tbody>
              {q.data?.items.map((d) => (
                <tr key={d.id}>
                  <td>
                    <Link to={`/devices/${d.id}`} className="id">{d.name}</Link>
                    <small>{d.id}</small>
                  </td>
                  <td>
                    <span className="badge badge-muted">{d.role}</span>
                  </td>
                  <td>
                    <Status value={d.status} />
                  </td>
                  <td>{d.activeKeyCount} keys</td>
                  <td>
                    {d.callbackUrl ? (
                      <span title={d.callbackUrl}>Có ({new URL(d.callbackUrl).host})</span>
                    ) : (
                      <span style={{ color: 'var(--muted)' }}>Chưa cấu hình</span>
                    )}
                  </td>
                  <td><Time value={d.createdAt} /></td>
                </tr>
              ))}
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
      deviceQuery.refetch();
      qc.invalidateQueries({ queryKey: ['devices'] });
    },
    onError: (e) => setActionError(e instanceof ApiError ? e.code : 'Đổi tên thất bại.'),
  });

  const disableMutation = useMutation({
    mutationFn: () =>
      auth.request<void>(`/v1/devices/${id}/disable`, {
        method: 'POST',
      }),
    onSuccess: () => {
      deviceQuery.refetch();
      keysQuery.refetch();
      qc.invalidateQueries({ queryKey: ['devices'] });
    },
    onError: (e) => setActionError(e instanceof ApiError ? e.code : 'Không thể vô hiệu hóa.'),
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
    onError: (e) => setActionError(e instanceof ApiError ? e.code : 'Không thể tạo API key.'),
  });

  const revokeKeyMutation = useMutation({
    mutationFn: (keyId: string) =>
      auth.request<void>(`/v1/devices/${id}/api-keys/${keyId}`, {
        method: 'DELETE',
      }),
    onSuccess: () => {
      keysQuery.refetch();
      deviceQuery.refetch();
      qc.invalidateQueries({ queryKey: ['devices'] });
    },
    onError: (e) => setActionError(e instanceof ApiError ? e.code : 'Không thể thu hồi API key.'),
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
    onError: (e) => setActionError(e instanceof ApiError ? e.code : 'Cấu hình callback thất bại.'),
  });

  const clearCallbackMutation = useMutation({
    mutationFn: () =>
      auth.request<void>(`/v1/devices/${id}/callback`, {
        method: 'DELETE',
      }),
    onSuccess: () => {
      deviceQuery.refetch();
    },
    onError: (e) => setActionError(e instanceof ApiError ? e.code : 'Xóa callback thất bại.'),
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
      pushQuery.refetch();
    },
    onError: (e) => setActionError(e instanceof ApiError ? e.code : 'Đăng ký push token thất bại.'),
  });

  const revokePushMutation = useMutation({
    mutationFn: () =>
      auth.request<void>(`/v1/devices/${id}/push-endpoint`, {
        method: 'DELETE',
      }),
    onSuccess: () => {
      pushQuery.refetch();
    },
    onError: (e) => setActionError(e instanceof ApiError ? e.code : 'Hủy push token thất bại.'),
  });

  const copyToClipboard = (text: string, label: string) => {
    navigator.clipboard.writeText(text);
    setCopyNotice(`Đã sao chép ${label}!`);
    setTimeout(() => setCopyNotice(''), 3000);
  };

  if (deviceQuery.isLoading) return <p>Đang tải thông tin thiết bị…</p>;
  if (deviceQuery.error || !deviceQuery.data) return <div className="error">Không tìm thấy thiết bị.</div>;

  const d = deviceQuery.data;
  const push = pushQuery.data;

  return (
    <section>
      <button className="back ghost" onClick={() => nav('/devices')}>← Quay lại danh sách</button>

      <header className="page-head">
        <div>
          <div className="eyebrow">CHI TIẾT THIẾT BỊ</div>
          <h1>{d.name}</h1>
          <p>ID: <code>{d.id}</code></p>
        </div>
        <div className="actions">
          {d.status === 'active' && (
            <>
              <button className="ghost" onClick={() => { setRenameText(d.name); setIsRenaming(true); }}>Đổi tên</button>
              <button
                className="danger"
                disabled={disableMutation.isPending}
                onClick={() => {
                  if (confirm('Vô hiệu hóa thiết bị này? Mọi API Key thuộc thiết bị sẽ lập tức ngừng hoạt động.')) {
                    disableMutation.mutate();
                  }
                }}
              >
                Vô hiệu hóa
              </button>
            </>
          )}
        </div>
      </header>

      {copyNotice && <div className="success">{copyNotice}</div>}
      {actionError && <div className="error">{actionError}</div>}

      {/* Rename Modal */}
      {isRenaming && (
        <div className="modal-backdrop">
          <div className="modal">
            <div className="modal-head">
              <h2>Đổi tên thiết bị</h2>
              <button className="ghost" onClick={() => setIsRenaming(false)}>✕</button>
            </div>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                renameMutation.mutate(renameText);
              }}
            >
              <label>
                Tên mới
                <input
                  value={renameText}
                  onChange={(e) => setRenameText(e.target.value)}
                  required
                  maxLength={100}
                />
              </label>
              <div className="modal-actions">
                <button type="button" className="ghost" onClick={() => setIsRenaming(false)}>Hủy</button>
                <button disabled={renameMutation.isPending}>Lưu thay đổi</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* New API Key Created Modal */}
      {newCreatedKey && (
        <div className="modal-backdrop">
          <div className="modal">
            <div className="modal-head">
              <h2>Đã tạo API Key mới</h2>
              <button className="ghost" onClick={() => setNewCreatedKey(null)}>✕</button>
            </div>
            <p style={{ color: 'var(--warning)', fontWeight: 600 }}>
              ⚠️ Lưu ý: Đây là lần duy nhất mã API Key hiển thị đầy đủ. Vui lòng sao chép và lưu trữ an toàn ngay bây giờ.
            </p>
            <div className="key-box">
              <span>{newCreatedKey.key}</span>
              <button type="button" onClick={() => copyToClipboard(newCreatedKey.key, 'API Key')}>Sao chép</button>
            </div>
            <div className="modal-actions">
              <button onClick={() => setNewCreatedKey(null)}>Đã lưu xong</button>
            </div>
          </div>
        </div>
      )}

      {/* Push Token Config Modal */}
      {showPushModal && (
        <div className="modal-backdrop">
          <div className="modal">
            <div className="modal-head">
              <h2>Đăng ký Push Token cho Mobile App</h2>
              <button className="ghost" onClick={() => setShowPushModal(false)}>✕</button>
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
              <p>Nhập mã token do Firebase (FCM) hoặc Apple (APNs) cấp cho ứng dụng trên điện thoại.</p>
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
                  {registerPushMutation.isPending ? 'Đang lưu…' : 'Lưu Token'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Callback Config Modal */}
      {showCallbackModal && (
        <div className="modal-backdrop">
          <div className="modal">
            <div className="modal-head">
              <h2>Cấu hình Webhook Callback</h2>
              <button className="ghost" onClick={() => { setShowCallbackModal(false); setNewCallbackResult(null); }}>✕</button>
            </div>
            {!newCallbackResult ? (
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  configCallbackMutation.mutate(callbackUrlInput);
                }}
              >
                <p>Nhập URL HTTP/HTTPS để nhận callback trạng thái gửi <code>notification.completed</code>.</p>
                <label>
                  Webhook Callback URL
                  <input
                    type="url"
                    placeholder="https://your-api.com/webhooks/notification"
                    value={callbackUrlInput}
                    onChange={(e) => setCallbackUrlInput(e.target.value)}
                    required
                  />
                </label>
                <div className="modal-actions">
                  <button type="button" className="ghost" onClick={() => setShowCallbackModal(false)}>Hủy</button>
                  <button disabled={configCallbackMutation.isPending}>Lưu và Sinh Secret</button>
                </div>
              </form>
            ) : (
              <div>
                <p className="success">Cấu hình Callback thành công!</p>
                <p style={{ color: 'var(--warning)', fontWeight: 600 }}>
                  ⚠️ Secret ký HMAC (dùng để xác thực chữ ký <code>X-Signature-SHA256</code> trên server của bạn):
                </p>
                <div className="key-box">
                  <span>{newCallbackResult.callbackSecret}</span>
                  <button type="button" onClick={() => copyToClipboard(newCallbackResult.callbackSecret, 'HMAC Secret')}>Sao chép</button>
                </div>
                <div className="modal-actions">
                  <button onClick={() => { setShowCallbackModal(false); setNewCallbackResult(null); }}>Đóng</button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      <div className="grid">
        <article className="card">
          <h2>Thông tin chung</h2>
          <dl>
            <dt>Vai trò</dt>
            <dd><span className="badge badge-muted">{d.role}</span></dd>
            <dt>Trạng thái</dt>
            <dd><Status value={d.status} /></dd>
            <dt>User sở hữu</dt>
            <dd><code>{d.userId}</code></dd>
            <dt>Ngày tạo</dt>
            <dd><Time value={d.createdAt} /></dd>
            <dt>Cập nhật</dt>
            <dd><Time value={d.updatedAt} /></dd>
          </dl>
        </article>

        <article className="card">
          <h2>Mobile Push Endpoint</h2>
          {push ? (
            <div>
              <dl>
                <dt>Nền tảng</dt>
                <dd>
                  <strong>{push.platform.toUpperCase()}</strong> ({push.platform === 'fcm' ? 'Android / Firebase' : 'iOS / APNs'})
                </dd>
                <dt>Trạng thái Push</dt>
                <dd><Status value={push.status} /></dd>
                <dt>Token mã hóa</dt>
                <dd>•••••••• (AES-256-GCM bảo mật)</dd>
                {push.lastDeliveredAt && (
                  <>
                    <dt>Lần gửi gần nhất</dt>
                    <dd><Time value={push.lastDeliveredAt} /></dd>
                  </>
                )}
              </dl>
              <div className="actions" style={{ marginTop: '18px' }}>
                <button className="ghost" onClick={() => setShowPushModal(true)}>Cập nhật Token</button>
                {push.status === 'active' && (
                  <button
                    className="ghost danger"
                    disabled={revokePushMutation.isPending}
                    onClick={() => {
                      if (confirm('Vô hiệu hóa Push Endpoint của thiết bị này?')) {
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
              <p>Chưa đăng ký Push Token. Thiết bị di động có thể đăng ký FCM/APNs token để nhận thông báo đẩy trực tiếp.</p>
              <button className="ghost" onClick={() => setShowPushModal(true)}>
                + Đăng ký Push Token
              </button>
            </div>
          )}
        </article>

        <article className="card">
          <h2>Webhook Callback (HMAC)</h2>
          {d.callbackUrl ? (
            <div>
              <dl>
                <dt>Callback URL</dt>
                <dd><code>{d.callbackUrl}</code></dd>
                <dt>HMAC Secret</dt>
                <dd>{d.hasCallbackSecret ? 'Đã kích hoạt bảo mật' : 'Chưa có'}</dd>
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
                    if (confirm('Xóa cấu hình callback webhook?')) {
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
              <p>Chưa cấu hình callback. Khi có callback, worker sẽ tự động đẩy kết quả hoàn tất về hệ thống của bạn.</p>
              <button
                className="ghost"
                onClick={() => {
                  setCallbackUrlInput('');
                  setShowCallbackModal(true);
                }}
              >
                Cấu hình Callback
              </button>
            </div>
          )}
        </article>
      </div>

      <div style={{ marginTop: '28px' }}>
        <header className="page-head">
          <div>
            <h2>Danh sách API Keys</h2>
            <p>Các key dùng để xác thực các request <code>POST /v1/notifications</code> từ thiết bị này.</p>
          </div>
          {d.status === 'active' && (
            <button
              disabled={createKeyMutation.isPending}
              onClick={() => createKeyMutation.mutate()}
            >
              + Tạo API Key mới
            </button>
          )}
        </header>

        {keysQuery.isLoading ? (
          <p>Đang tải danh sách keys…</p>
        ) : keysQuery.data?.items.length === 0 ? (
          <div className="empty">
            <p>Chưa có API Key nào được tạo cho thiết bị này.</p>
          </div>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Prefix Key</th>
                  <th>Trạng thái</th>
                  <th>Ngày tạo</th>
                  <th>Hành động</th>
                </tr>
              </thead>
              <tbody>
                {keysQuery.data?.items.map((k) => (
                  <tr key={k.id}>
                    <td>
                      <code>{k.keyPrefix}...</code>
                    </td>
                    <td>
                      <Status value={k.status} />
                    </td>
                    <td><Time value={k.createdAt} /></td>
                    <td>
                      {k.status === 'active' && (
                        <button
                          className="ghost danger"
                          disabled={revokeKeyMutation.isPending}
                          onClick={() => {
                            if (confirm(`Thu hồi API key [${k.keyPrefix}...]? Key sẽ ngừng hoạt động ngay lập tức.`)) {
                              revokeKeyMutation.mutate(k.id);
                            }
                          }}
                        >
                          Thu hồi
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
