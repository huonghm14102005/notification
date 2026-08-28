import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../shared/types';
import { Status, Time } from '../notifications/Status';

type Template = {
  id: string;
  templateCode: string;
  scope: 'tenant' | 'source';
  sourceDeviceId?: string;
  audience: 'user' | 'system';
  subject: string;
  textBody?: string;
  htmlBody?: string;
  variables: string[];
  status: 'draft' | 'active' | 'retired';
  createdAt: string;
  updatedAt: string;
};

type TemplatePage = {
  items: Template[];
  nextCursor?: string;
};

export function TemplateList() {
  const auth = useAuth();
  const qc = useQueryClient();
  const [showCreate, setShowCreate] = useState(false);
  const [statusFilter, setStatusFilter] = useState('');
  const [scopeFilter, setScopeFilter] = useState('');
  const [audienceFilter, setAudienceFilter] = useState('');
  const [error, setError] = useState('');

  const q = useQuery({
    queryKey: ['templates', statusFilter, scopeFilter, audienceFilter],
    queryFn: () => {
      const params = new URLSearchParams();
      if (statusFilter) params.set('status', statusFilter);
      if (scopeFilter) params.set('scope', scopeFilter);
      if (audienceFilter) params.set('audience', audienceFilter);
      return auth.request<TemplatePage>(`/v1/templates?${params.toString()}`);
    },
  });

  const create = useMutation({
    mutationFn: (data: {
      templateCode: string;
      scope: 'tenant' | 'source';
      audience: 'user' | 'system';
      subject: string;
      textBody?: string;
      htmlBody?: string;
      variables: string[];
    }) =>
      auth.request<Template>('/v1/templates', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      }),
    onSuccess: () => {
      setShowCreate(false);
      qc.invalidateQueries({ queryKey: ['templates'] });
    },
    onError: (e) => {
      setError(e instanceof ApiError ? `Lỗi: ${e.code}` : 'Không thể tạo template.');
    },
  });

  return (
    <section>
      <header className="page-head">
        <div>
          <div className="eyebrow">NỘI DUNG & MẪU</div>
          <h1>Mẫu thông báo (Templates)</h1>
          <p>Quản lý mẫu nội dung email, hỗ trợ plain-text, HTML và phiên bản bất biến.</p>
        </div>
        <button onClick={() => { setError(''); setShowCreate(true); }}>Tạo Template mới</button>
      </header>

      <div className="filters">
        <label>
          Trạng thái
          <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
            <option value="">Tất cả</option>
            <option value="active">Đang hoạt động</option>
            <option value="draft">Bản nháp</option>
            <option value="retired">Đã ngừng dùng</option>
          </select>
        </label>
        <label>
          Phạm vi
          <select value={scopeFilter} onChange={(e) => setScopeFilter(e.target.value)}>
            <option value="">Tất cả</option>
            <option value="tenant">Tenant</option>
            <option value="source">Source Device</option>
          </select>
        </label>
        <label>
          Đối tượng (Audience)
          <select value={audienceFilter} onChange={(e) => setAudienceFilter(e.target.value)}>
            <option value="">Tất cả</option>
            <option value="user">User (Khách hàng)</option>
            <option value="system">System (Hệ thống/Kỹ thuật)</option>
          </select>
        </label>
      </div>

      {showCreate && (
        <div className="modal-backdrop">
          <div className="modal" style={{ maxWidth: '640px' }}>
            <div className="modal-head">
              <h2>Tạo Template mới</h2>
              <button className="ghost" onClick={() => setShowCreate(false)}>✕</button>
            </div>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                const d = new FormData(e.currentTarget);
                const rawVars = String(d.get('variables') || '');
                const variables = rawVars
                  .split(',')
                  .map((v) => v.trim())
                  .filter(Boolean);

                create.mutate({
                  templateCode: String(d.get('templateCode')),
                  scope: d.get('scope') as 'tenant' | 'source',
                  audience: d.get('audience') as 'user' | 'system',
                  subject: String(d.get('subject')),
                  textBody: String(d.get('textBody') || ''),
                  htmlBody: String(d.get('htmlBody') || ''),
                  variables,
                });
              }}
            >
              <label>
                Mã Template (Template Code)
                <input name="templateCode" placeholder="vd: welcome_email, order_success" required maxLength={100} />
              </label>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                <label>
                  Phạm vi
                  <select name="scope" defaultValue="tenant">
                    <option value="tenant">Toàn bộ Tenant</option>
                    <option value="source">Chỉ Device nguồn</option>
                  </select>
                </label>
                <label>
                  Đối tượng
                  <select name="audience" defaultValue="user">
                    <option value="user">User (Khách hàng)</option>
                    <option value="system">System (Kỹ thuật)</option>
                  </select>
                </label>
              </div>
              <label>
                Tiêu đề email (Subject)
                <input name="subject" placeholder="vd: Chào mừng {{name}} đến với hệ thống!" required />
              </label>
              <label>
                Danh sách biến (Cách nhau bằng dấu phẩy)
                <input name="variables" placeholder="vd: name, code, order_id" />
              </label>
              <label>
                Nội dung Plain-Text
                <textarea name="textBody" placeholder="Xin chào {{name}}, mã của bạn là {{code}}." rows={3} />
              </label>
              <label>
                Nội dung HTML (Tùy chọn)
                <textarea name="htmlBody" placeholder="<h1>Xin chào {{name}}</h1><p>Mã của bạn: <b>{{code}}</b></p>" rows={4} />
              </label>

              {error && <div className="error" role="alert">{error}</div>}
              <div className="modal-actions">
                <button type="button" className="ghost" onClick={() => setShowCreate(false)}>Hủy</button>
                <button disabled={create.isPending}>Tạo Template</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {q.isLoading ? (
        <p>Đang tải danh sách templates…</p>
      ) : q.error ? (
        <div className="error">Không tải được danh sách template.</div>
      ) : q.data?.items.length === 0 ? (
        <div className="empty">
          <h2>Chưa có Template nào</h2>
          <p>Tạo template đầu tiên để chuẩn hóa nội dung gửi thông báo.</p>
        </div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Mã Template</th>
                <th>Tiêu đề (Subject)</th>
                <th>Phạm vi</th>
                <th>Đối tượng</th>
                <th>Biến số</th>
                <th>Trạng thái</th>
                <th>Ngày tạo</th>
              </tr>
            </thead>
            <tbody>
              {q.data?.items.map((t) => (
                <tr key={t.id}>
                  <td>
                    <Link to={`/templates/${t.id}`} className="id">{t.templateCode}</Link>
                    <small>{t.id}</small>
                  </td>
                  <td>
                    <strong>{t.subject}</strong>
                  </td>
                  <td>
                    <span className="badge badge-muted">{t.scope}</span>
                  </td>
                  <td>
                    <span className="badge badge-muted">{t.audience}</span>
                  </td>
                  <td>
                    {t.variables && t.variables.length > 0 ? (
                      <code>{t.variables.join(', ')}</code>
                    ) : (
                      <span style={{ color: 'var(--muted)' }}>Không</span>
                    )}
                  </td>
                  <td>
                    <Status value={t.status} />
                  </td>
                  <td><Time value={t.createdAt} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

export function TemplateDetail() {
  const { id } = useParams();
  const auth = useAuth();
  const nav = useNavigate();
  const qc = useQueryClient();

  const [activeTab, setActiveTab] = useState<'editor' | 'preview'>('editor');
  const [testVars, setTestVars] = useState<Record<string, string>>({});
  const [actionError, setActionError] = useState('');
  const [successNotice, setSuccessNotice] = useState('');

  const q = useQuery({
    queryKey: ['template', id],
    queryFn: () => auth.request<Template>(`/v1/templates/${id}`),
  });

  const updateMutation = useMutation({
    mutationFn: (data: {
      subject: string;
      textBody?: string;
      htmlBody?: string;
      variables?: string[];
    }) =>
      auth.request<Template>(`/v1/templates/${id}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      }),
    onSuccess: () => {
      setSuccessNotice('Đã lưu nội dung template thành công!');
      setTimeout(() => setSuccessNotice(''), 3000);
      q.refetch();
      qc.invalidateQueries({ queryKey: ['templates'] });
    },
    onError: (e) => setActionError(e instanceof ApiError ? e.code : 'Lưu thất bại.'),
  });

  const cloneMutation = useMutation({
    mutationFn: () =>
      auth.request<Template>(`/v1/templates/${id}/versions`, {
        method: 'POST',
      }),
    onSuccess: (newVersion) => {
      qc.invalidateQueries({ queryKey: ['templates'] });
      nav(`/templates/${newVersion.id}`);
    },
    onError: (e) => setActionError(e instanceof ApiError ? e.code : 'Không thể tạo bản sao phiên bản.'),
  });

  const publishMutation = useMutation({
    mutationFn: () =>
      auth.request<Template>(`/v1/templates/${id}/publish`, {
        method: 'POST',
      }),
    onSuccess: () => {
      setSuccessNotice('Đã phát hành (Publish) phiên bản thành công!');
      setTimeout(() => setSuccessNotice(''), 3000);
      q.refetch();
      qc.invalidateQueries({ queryKey: ['templates'] });
    },
    onError: (e) => setActionError(e instanceof ApiError ? e.code : 'Không thể phát hành template.'),
  });

  const retireMutation = useMutation({
    mutationFn: () =>
      auth.request<Template>(`/v1/templates/${id}/retire`, {
        method: 'POST',
      }),
    onSuccess: () => {
      setSuccessNotice('Đã chuyển template sang trạng thái ngừng dùng (Retired).');
      setTimeout(() => setSuccessNotice(''), 3000);
      q.refetch();
      qc.invalidateQueries({ queryKey: ['templates'] });
    },
    onError: (e) => setActionError(e instanceof ApiError ? e.code : 'Không thể retire template.'),
  });

  if (q.isLoading) return <p>Đang tải chi tiết template…</p>;
  if (q.error || !q.data) return <div className="error">Không tìm thấy template.</div>;

  const t = q.data;

  const renderText = (content?: string) => {
    if (!content) return '';
    let rendered = content;
    for (const [k, v] of Object.entries(testVars)) {
      rendered = rendered.replaceAll(`{{${k}}}`, v || `{{${k}}}`);
    }
    return rendered;
  };

  return (
    <section>
      <button className="back ghost" onClick={() => nav('/templates')}>← Quay lại danh sách</button>

      <header className="page-head">
        <div>
          <div className="eyebrow">CHI TIẾT TEMPLATE</div>
          <h1>{t.templateCode}</h1>
          <p>Mã phiên bản: <code>{t.id}</code></p>
        </div>
        <div className="actions">
          {t.status === 'draft' && (
            <button
              className="success"
              style={{ background: '#166534', color: 'white' }}
              disabled={publishMutation.isPending}
              onClick={() => publishMutation.mutate()}
            >
              ✓ Phát hành (Publish)
            </button>
          )}
          {t.status === 'active' && (
            <>
              <button
                className="ghost"
                disabled={cloneMutation.isPending}
                onClick={() => cloneMutation.mutate()}
              >
                + Tạo phiên bản nháp mới
              </button>
              <button
                className="danger"
                disabled={retireMutation.isPending}
                onClick={() => {
                  if (confirm('Ngừng dùng (Retire) phiên bản template này?')) {
                    retireMutation.mutate();
                  }
                }}
              >
                Ngừng dùng (Retire)
              </button>
            </>
          )}
          {t.status === 'retired' && (
            <button
              className="ghost"
              disabled={cloneMutation.isPending}
              onClick={() => cloneMutation.mutate()}
            >
              + Nhân bản thành nháp mới
            </button>
          )}
        </div>
      </header>

      {successNotice && <div className="success">{successNotice}</div>}
      {actionError && <div className="error">{actionError}</div>}

      <div className="tabs">
        <button
          className={`tab ${activeTab === 'editor' ? 'active' : ''}`}
          onClick={() => setActiveTab('editor')}
        >
          Trình chỉnh sửa nội dung
        </button>
        <button
          className={`tab ${activeTab === 'preview' ? 'active' : ''}`}
          onClick={() => setActiveTab('preview')}
        >
          Xem trước (Live Preview & Variables)
        </button>
      </div>

      {activeTab === 'editor' ? (
        <div className="grid">
          <article className="card">
            <h2>Nội dung Template</h2>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                const d = new FormData(e.currentTarget);
                const rawVars = String(d.get('variables') || '');
                const variables = rawVars
                  .split(',')
                  .map((v) => v.trim())
                  .filter(Boolean);

                updateMutation.mutate({
                  subject: String(d.get('subject')),
                  textBody: String(d.get('textBody') || ''),
                  htmlBody: String(d.get('htmlBody') || ''),
                  variables,
                });
              }}
            >
              <label>
                Tiêu đề email (Subject)
                <input
                  name="subject"
                  defaultValue={t.subject}
                  disabled={t.status !== 'draft'}
                  required
                />
              </label>
              <label>
                Danh sách biến (phân tách bởi dấu phẩy)
                <input
                  name="variables"
                  defaultValue={t.variables?.join(', ') || ''}
                  disabled={t.status !== 'draft'}
                />
              </label>
              <label>
                Nội dung Plain-Text
                <textarea
                  name="textBody"
                  defaultValue={t.textBody || ''}
                  disabled={t.status !== 'draft'}
                  rows={5}
                />
              </label>
              <label>
                Nội dung HTML (Tùy chọn)
                <textarea
                  name="htmlBody"
                  defaultValue={t.htmlBody || ''}
                  disabled={t.status !== 'draft'}
                  rows={8}
                />
              </label>

              {t.status === 'draft' ? (
                <div className="modal-actions" style={{ marginTop: '18px' }}>
                  <button disabled={updateMutation.isPending}>Lưu bản nháp</button>
                </div>
              ) : (
                <p style={{ color: 'var(--muted)', fontSize: '0.85rem', marginTop: '12px' }}>
                  * Phiên bản đã phát hành là bất biến. Hãy bấm <strong>Tạo phiên bản nháp mới</strong> để chỉnh sửa.
                </p>
              )}
            </form>
          </article>

          <article className="card">
            <h2>Thông tin phiên bản</h2>
            <dl>
              <dt>Trạng thái</dt>
              <dd><Status value={t.status} /></dd>
              <dt>Phạm vi</dt>
              <dd><span className="badge badge-muted">{t.scope}</span></dd>
              <dt>Đối tượng</dt>
              <dd><span className="badge badge-muted">{t.audience}</span></dd>
              <dt>Biến số hợp lệ</dt>
              <dd>
                {t.variables && t.variables.length > 0 ? (
                  <code>{t.variables.join(', ')}</code>
                ) : (
                  'Không có'
                )}
              </dd>
              <dt>Ngày tạo</dt>
              <dd><Time value={t.createdAt} /></dd>
              <dt>Cập nhật</dt>
              <dd><Time value={t.updatedAt} /></dd>
            </dl>
          </article>
        </div>
      ) : (
        <div className="grid">
          <article className="card">
            <h2>Nhập biến thử nghiệm</h2>
            {t.variables && t.variables.length > 0 ? (
              <div style={{ display: 'grid', gap: '10px' }}>
                {t.variables.map((v) => (
                  <label key={v}>
                    Biến <code>{`{{${v}}}`}</code>:
                    <input
                      placeholder={`Giá trị cho ${v}...`}
                      value={testVars[v] || ''}
                      onChange={(e) =>
                        setTestVars((prev) => ({ ...prev, [v]: e.target.value }))
                      }
                    />
                  </label>
                ))}
              </div>
            ) : (
              <p style={{ color: 'var(--muted)' }}>Template này không khai báo biến số nào.</p>
            )}
          </article>

          <article className="card">
            <h2>Kết quả kết xuất (Render Preview)</h2>
            <div style={{ marginBottom: '14px' }}>
              <div className="eyebrow">SUBJECT ĐÃ RENDER:</div>
              <p style={{ fontWeight: 700, fontSize: '1.1rem', margin: '4px 0 16px 0' }}>
                {renderText(t.subject)}
              </p>
            </div>

            {t.htmlBody ? (
              <div>
                <div className="eyebrow" style={{ marginBottom: '6px' }}>HTML PREVIEW:</div>
                <div className="preview-pane">
                  <iframe
                    title="html-preview"
                    srcDoc={renderText(t.htmlBody)}
                    sandbox="allow-same-origin"
                  />
                </div>
              </div>
            ) : null}

            {t.textBody ? (
              <div style={{ marginTop: '16px' }}>
                <div className="eyebrow" style={{ marginBottom: '6px' }}>PLAIN-TEXT PREVIEW:</div>
                <pre style={{ background: '#f5f7f4', padding: '14px', borderRadius: '8px' }}>
                  {renderText(t.textBody)}
                </pre>
              </div>
            ) : null}
          </article>
        </div>
      )}
    </section>
  );
}
