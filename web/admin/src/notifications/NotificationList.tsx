import { useState, useMemo } from 'react';
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useSearchParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import type { Page } from '../shared/types';
import { Status, Time } from './Status';
import { ApiError } from '../shared/types';

export function NotificationList() {
  const auth = useAuth();
  const qc = useQueryClient();
  const [params, setParams] = useSearchParams();
  const [showDispatchModal, setShowDispatchModal] = useState(false);
  const [dispatchResult, setDispatchResult] = useState<{ id?: string; error?: string } | null>(null);

  const filter = params.toString();
  const q = useInfiniteQuery({
    queryKey: ['notifications', filter],
    initialPageParam: '',
    queryFn: ({ pageParam }) => {
      const p = new URLSearchParams(params);
      if (pageParam) p.set('cursor', pageParam);
      return auth.request<Page>(`/v1/notifications?${p}`);
    },
    getNextPageParam: (x) => x.nextCursor ?? undefined,
  });

  const items = useMemo(() => q.data?.pages.flatMap((x) => x.items) ?? [], [q.data]);

  const setFilter = (k: string, v: string) => {
    const p = new URLSearchParams(params);
    if (v) p.set(k, v);
    else p.delete(k);
    p.delete('cursor');
    setParams(p);
  };

  const channelIcon = (ch?: string) => {
    switch (ch?.toLowerCase()) {
      case 'telegram':
        return '✈️ Telegram';
      case 'discord':
        return '🎮 Discord';
      case 'push':
        return '📱 Push';
      default:
        return '✉️ Email';
    }
  };

  return (
    <>
      <header className="page-head">
        <div>
          <div className="eyebrow">OPERATIONS</div>
          <h1>Lịch sử thông báo</h1>
          <p>Theo dõi trạng thái gửi đa kênh (Email, Telegram, Discord, Push Mobile) và xử lý sự cố.</p>
        </div>
        <div className="actions">
          <button className="ghost" onClick={() => q.refetch()}>
            Làm mới
          </button>
          <button onClick={() => { setDispatchResult(null); setShowDispatchModal(true); }}>
            + Gửi thông báo thử nghiệm
          </button>
        </div>
      </header>

      {/* Quick Dispatch Modal */}
      {showDispatchModal && (
        <DispatchModal
          onClose={() => setShowDispatchModal(false)}
          onSuccess={(id) => {
            setDispatchResult({ id });
            qc.invalidateQueries({ queryKey: ['notifications'] });
          }}
        />
      )}

      <section className="filters" aria-label="Bộ lọc">
        <label>
          Trạng thái
          <select value={params.get('status') ?? ''} onChange={(e) => setFilter('status', e.target.value)}>
            <option value="">Tất cả</option>
            <option value="failed">Thất bại</option>
            <option value="partially_delivered">Gửi một phần</option>
            <option value="delivered">Đã gửi</option>
            <option value="accepted">Đã tiếp nhận</option>
            <option value="processing">Đang gửi</option>
            <option value="cancelled">Đã hủy</option>
          </select>
        </label>
        <label>
          Kênh
          <select value={params.get('channel') ?? ''} onChange={(e) => setFilter('channel', e.target.value)}>
            <option value="">Tất cả kênh</option>
            <option value="email">✉️ Email</option>
            <option value="telegram">✈️ Telegram</option>
            <option value="discord">🎮 Discord</option>
            <option value="push">📱 Push Mobile</option>
          </select>
        </label>
        <label>
          Device ID
          <input
            value={params.get('sourceDeviceId') ?? ''}
            onChange={(e) => setFilter('sourceDeviceId', e.target.value)}
            placeholder="UUID thiết bị nguồn"
          />
        </label>
        <label>
          API key ID
          <input
            value={params.get('apiKeyId') ?? ''}
            onChange={(e) => setFilter('apiKeyId', e.target.value)}
            placeholder="UUID khóa API"
          />
        </label>
      </section>

      {q.isPending ? (
        <div className="skeleton" aria-label="Đang tải danh sách" />
      ) : q.isError ? (
        <div className="empty">
          <h2>Không tải được dữ liệu</h2>
          <button onClick={() => q.refetch()}>Thử lại</button>
        </div>
      ) : items.length === 0 ? (
        <div className="empty">
          <h2>Chưa có thông báo nào</h2>
          <p>Thay đổi bộ lọc hoặc sử dụng nút "Gửi thông báo thử nghiệm" để gửi thông báo đầu tiên.</p>
        </div>
      ) : (
        <section className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Thông báo</th>
                <th>Nguồn phát</th>
                <th>Trạng thái</th>
                <th>Kênh & Người nhận</th>
                <th>Cập nhật</th>
              </tr>
            </thead>
            <tbody>
              {items.map((x) => (
                <tr key={x.id}>
                  <td data-label="Thông báo">
                    <Link to={`/notifications/${x.id}`} className="id">
                      {x.id.slice(0, 8)}
                    </Link>
                    <small>{x.id}</small>
                  </td>
                  <td data-label="Nguồn">{x.producerName}</td>
                  <td data-label="Trạng thái">
                    <Status value={x.status} />
                  </td>
                  <td data-label="Kênh & Người nhận">
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                      {x.deliveries.map((d) => (
                        <span key={d.id} style={{ fontSize: '0.85rem' }}>
                          <strong>{channelIcon(d.channel)}:</strong> <code>{d.target}</code>
                          {d.targetRef && <small style={{ marginLeft: '4px', color: 'var(--muted)' }}>({d.targetRef})</small>}
                        </span>
                      ))}
                    </div>
                  </td>
                  <td data-label="Cập nhật">
                    <Time value={x.updatedAt} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {q.hasNextPage && (
            <button
              className="load"
              disabled={q.isFetchingNextPage}
              onClick={() => q.fetchNextPage()}
            >
              {q.isFetchingNextPage ? 'Đang tải…' : 'Tải thêm'}
            </button>
          )}
        </section>
      )}
    </>
  );
}

function DispatchModal({ onClose, onSuccess }: { onClose: () => void; onSuccess: (id: string) => void }) {
  const auth = useAuth();
  const [contentMode, setContentMode] = useState<'plaintext' | 'template'>('plaintext');
  const [channel, setChannel] = useState<'email' | 'telegram' | 'discord' | 'push'>('push');
  const [target, setTarget] = useState('');
  const [subject, setSubject] = useState('Thông báo thử nghiệm');
  const [body, setBody] = useState('Nội dung gửi thử qua giao diện Admin Console.');
  const [senderKey, setSenderKey] = useState('');
  const [selectedTemplateCode, setSelectedTemplateCode] = useState('');
  const [templateVars, setTemplateVars] = useState<Record<string, string>>({});
  const [error, setError] = useState('');

  // Fetch active devices to perform direct push dispatch
  const devicesQuery = useQuery({
    queryKey: ['devices'],
    queryFn: () => auth.request<{ items: Array<{ id: string; name: string }> }>('/v1/devices'),
  });

  // Fetch active templates
  const templatesQuery = useQuery({
    queryKey: ['active-templates'],
    queryFn: () => auth.request<{ items: Array<{ id: string; templateCode: string; subject: string; variables?: string[] }> }>('/v1/templates?status=active'),
  });

  const selectedTemplate = templatesQuery.data?.items.find(t => t.templateCode === selectedTemplateCode);

  const handleTemplateSelect = (code: string) => {
    setSelectedTemplateCode(code);
    const tmpl = templatesQuery.data?.items.find(t => t.templateCode === code);
    if (tmpl && tmpl.variables) {
      const initial: Record<string, string> = {};
      tmpl.variables.forEach(v => {
        if (v.toLowerCase().includes('name')) initial[v] = 'Nguyễn Văn A';
        else if (v.toLowerCase().includes('order')) initial[v] = 'DH-2026-999';
        else if (v.toLowerCase().includes('otp') || v.toLowerCase().includes('code')) initial[v] = '686868';
        else initial[v] = `Dữ liệu mẫu cho ${v}`;
      });
      setTemplateVars(initial);
    } else {
      setTemplateVars({});
    }
  };

  const dispatchMutation = useMutation({
    mutationFn: async () => {
      setError('');
      // Intake request payload
      const payload: any = {
        senderKey: senderKey.trim() ? senderKey.trim() : undefined,
        channels: [
          {
            type: channel,
            targets: [{ address: target.trim(), ref: 'admin-test' }],
          },
        ],
      };

      if (contentMode === 'template') {
        if (!selectedTemplateCode) throw new Error('Vui lòng chọn một mẫu thông báo (template).');
        payload.content = {
          mode: 'template',
          templateCode: selectedTemplateCode,
          data: templateVars,
        };
      } else {
        payload.content = {
          mode: 'plaintext',
          subject: subject.trim(),
          body: body.trim(),
        };
      }

      // Call intake endpoint
      return auth.request<{ id: string; status: string; deliveries: Array<{ id: string; channel: string }> }>(
        '/v1/notifications',
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(payload),
        }
      );
    },
    onSuccess: (data) => {
      onSuccess(data.id);
      onClose();
    },
    onError: (e: any) => {
      setError(e instanceof ApiError ? `Lỗi (${e.code}): Không thể gửi thông báo` : (e.message || 'Yêu cầu gửi thất bại.'));
    },
  });

  return (
    <div className="modal-backdrop">
      <div className="modal" style={{ maxWidth: '620px' }}>
        <div className="modal-head">
          <div>
            <h2>Gửi thông báo trực tiếp (Dispatch Playground)</h2>
            <p style={{ margin: '4px 0 0', fontSize: '0.85rem', color: 'var(--muted)' }}>
              Kiểm thử gửi thông báo qua Email, Telegram, Discord hoặc Push Mobile bằng Plaintext hoặc Mẫu (Template).
            </p>
          </div>
          <button className="ghost" onClick={onClose}>✕</button>
        </div>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            dispatchMutation.mutate();
          }}
        >
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
            <label>
              Chế độ nội dung
              <select
                value={contentMode}
                onChange={(e) => {
                  const m = e.target.value as 'plaintext' | 'template';
                  setContentMode(m);
                  if (m === 'template' && !selectedTemplateCode && templatesQuery.data?.items[0]) {
                    handleTemplateSelect(templatesQuery.data.items[0].templateCode);
                  }
                }}
              >
                <option value="plaintext">Văn bản trực tiếp (Plaintext)</option>
                <option value="template">Mẫu nội dung (Template)</option>
              </select>
            </label>

            <label>
              Kênh gửi (Channel)
              <select
                value={channel}
                onChange={(e) => {
                  const c = e.target.value as 'email' | 'telegram' | 'discord' | 'push';
                  setChannel(c);
                  if (c === 'push') setTarget(devicesQuery.data?.items[0]?.id || '');
                  else if (c === 'email') setTarget('huong102145@st.vimaru.edu.vn');
                  else if (c === 'telegram') setTarget('');
                  else if (c === 'discord') setTarget('');
                }}
              >
                <option value="push">📱 Push Mobile (Device ID)</option>
                <option value="telegram">✈️ Telegram (Chat ID)</option>
                <option value="discord">🎮 Discord (Webhook URL)</option>
                <option value="email">✉️ Email (SMTP / Native HTTPS)</option>
              </select>
            </label>
          </div>

          <label>
            {channel === 'email'
              ? 'Địa chỉ Email người nhận'
              : channel === 'telegram'
              ? 'Telegram Target (Chat ID hoặc Token:ChatID)'
              : channel === 'discord'
              ? 'Discord Webhook URL'
              : 'Target Device ID (UUID)'}
            <input
              value={target}
              onChange={(e) => setTarget(e.target.value)}
              placeholder={
                channel === 'email'
                  ? 'user@example.com'
                  : channel === 'telegram'
                  ? 'vd: 123456789 hoặc 123456:ABC-DEF:987654'
                  : channel === 'discord'
                  ? 'https://discord.com/api/webhooks/...'
                  : 'UUID của thiết bị nhận (vd: 8fa1b439-...)'
              }
              required
            />
          </label>

          <label>
            Sender Key (Tùy chọn, bỏ trống để dùng mặc định)
            <input
              value={senderKey}
              onChange={(e) => setSenderKey(e.target.value)}
              placeholder="default"
            />
          </label>

          {contentMode === 'plaintext' ? (
            <>
              <label>
                Tiêu đề thông báo (Subject)
                <input
                  value={subject}
                  onChange={(e) => setSubject(e.target.value)}
                  required
                />
              </label>

              <label>
                Nội dung thông báo (Body)
                <textarea
                  rows={4}
                  value={body}
                  onChange={(e) => setBody(e.target.value)}
                  required
                />
              </label>
            </>
          ) : (
            <div style={{ background: '#f8fafc', padding: '12px', borderRadius: '8px', border: '1px solid var(--line)', marginBottom: '16px' }}>
              <label>
                Chọn Mẫu Đang Hoạt Động (Active Template)
                {templatesQuery.isLoading ? (
                  <p>Đang tải danh sách mẫu…</p>
                ) : !templatesQuery.data?.items?.length ? (
                  <p style={{ color: 'var(--color-danger, #ef4444)', fontSize: '0.9rem' }}>
                    Chưa có template nào ở trạng thái Active. Hãy vào mục "Mẫu thông báo" để xuất bản (Publish) một template trước.
                  </p>
                ) : (
                  <select
                    value={selectedTemplateCode}
                    onChange={(e) => handleTemplateSelect(e.target.value)}
                    required
                  >
                    <option value="">-- Chọn Template --</option>
                    {templatesQuery.data.items.map((t) => (
                      <option key={t.id} value={t.templateCode}>
                        {t.templateCode} — {t.subject}
                      </option>
                    ))}
                  </select>
                )}
              </label>

              {selectedTemplate && selectedTemplate.variables && selectedTemplate.variables.length > 0 && (
                <div style={{ marginTop: '12px' }}>
                  <div style={{ fontSize: '0.85rem', fontWeight: 600, marginBottom: '8px', color: 'var(--muted)' }}>
                    Biến số cần truyền (Template Variables):
                  </div>
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
                    {selectedTemplate.variables.map((v) => (
                      <label key={v} style={{ margin: 0, fontSize: '0.8rem' }}>
                        <code>{`{{${v}}}`}</code>
                        <input
                          style={{ marginTop: '4px' }}
                          value={templateVars[v] || ''}
                          onChange={(e) => setTemplateVars({ ...templateVars, [v]: e.target.value })}
                          placeholder={`Giá trị cho ${v}`}
                          required
                        />
                      </label>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}

          {error && <div className="error" role="alert">{error}</div>}

          <div className="modal-actions">
            <button type="button" className="ghost" onClick={onClose}>
              Hủy
            </button>
            <button disabled={dispatchMutation.isPending || !target.trim()}>
              {dispatchMutation.isPending ? 'Đang gửi…' : 'Gửi thông báo ngay'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
