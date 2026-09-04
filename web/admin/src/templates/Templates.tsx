import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../shared/types';
import { Status, Time } from '../notifications/Status';

export type Template = {
  id: string;
  templateCode: string;
  version?: number;
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

export type TemplatePage = {
  items: Template[];
  nextCursor?: string;
};

interface TemplatePreset {
  id: string;
  name: string;
  badge: string;
  templateCode: string;
  scope: 'tenant' | 'source';
  audience: 'user' | 'system';
  subject: string;
  variables: string;
  textBody: string;
  htmlBody: string;
}

const PRESETS: TemplatePreset[] = [
  {
    id: 'order-success',
    name: 'Xác nhận đơn hàng',
    badge: '📦 Đơn Hàng',
    templateCode: 'order-success',
    scope: 'tenant',
    audience: 'user',
    subject: 'Xác nhận đơn hàng #{{orderId}} thành công',
    variables: 'name, orderId, totalAmount',
    textBody: 'Xin chào {{name}}, đơn hàng #{{orderId}} trị giá {{totalAmount}} của bạn đã được thanh toán thành công!',
    htmlBody: `<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 560px; margin: 0 auto; padding: 24px; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px;">
  <div style="text-align: center; margin-bottom: 20px;">
    <span style="display: inline-block; padding: 10px 16px; background: #dcfce7; color: #15803d; border-radius: 999px; font-weight: 700; font-size: 14px;">
      ✓ ĐẶT HÀNG THÀNH CÔNG
    </span>
  </div>
  <h2 style="color: #0f172a; margin-top: 0; text-align: center;">Cảm ơn bạn đã mua hàng!</h2>
  <p style="color: #475569; font-size: 15px; line-height: 1.5;">
    Xin chào <strong>{{name}}</strong>, đơn hàng <strong>#{{orderId}}</strong> của bạn đã được hệ thống tiếp nhận và đang được chuẩn bị đóng gói.
  </p>
  <div style="background: #f8fafc; border: 1px dashed #cbd5e1; border-radius: 8px; padding: 16px; margin: 20px 0;">
    <div style="display: flex; justify-content: space-between; margin-bottom: 8px; font-size: 14px; color: #475569;">
      <span>Mã đơn hàng:</span>
      <strong style="color: #0f172a;">#{{orderId}}</strong>
    </div>
    <div style="display: flex; justify-content: space-between; font-size: 14px; color: #475569;">
      <span>Tổng thanh toán:</span>
      <strong style="color: #16a34a; font-size: 16px;">{{totalAmount}}</strong>
    </div>
  </div>
  <p style="color: #64746d; font-size: 13px; text-align: center; margin-top: 24px;">
    Nếu bạn có thắc mắc, vui lòng liên hệ bộ phận chăm sóc khách hàng của chúng tôi.
  </p>
</div>`,
  },
  {
    id: 'auth-otp',
    name: 'Mã xác thực OTP',
    badge: '🔐 Mã OTP',
    templateCode: 'auth-otp',
    scope: 'tenant',
    audience: 'user',
    subject: 'Mã xác thực đăng nhập của bạn: {{otpCode}}',
    variables: 'name, otpCode, expireMinutes',
    textBody: 'Xin chào {{name}}, mã xác thực OTP của bạn là {{otpCode}}. Mã có hiệu lực trong {{expireMinutes}} phút. Không chia sẻ mã này cho bất kỳ ai.',
    htmlBody: `<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 500px; margin: 0 auto; padding: 24px; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; text-align: center;">
  <h2 style="color: #0f172a; margin-top: 0;">Mã Xác Thực Bảo Mật (OTP)</h2>
  <p style="color: #475569; font-size: 15px;">
    Xin chào <strong>{{name}}</strong>, mã xác thực để đăng nhập tài khoản của bạn là:
  </p>
  <div style="background: #f1f5f9; border: 1px solid #cbd5e1; border-radius: 8px; padding: 18px; margin: 24px 0; font-size: 32px; font-weight: 800; letter-spacing: 8px; color: #0284c7; font-family: monospace;">
    {{otpCode}}
  </div>
  <p style="color: #dc2626; font-size: 13px; margin-bottom: 4px;">
    ⚠️ Mã chỉ có hiệu lực trong vòng <strong>{{expireMinutes}} phút</strong>.
  </p>
  <p style="color: #64746d; font-size: 13px; margin: 0;">
    Tuyệt đối không cung cấp mã số này cho người khác dưới mọi hình thức.
  </p>
</div>`,
  },
  {
    id: 'welcome-member',
    name: 'Chào mừng thành viên',
    badge: '🎉 Chào Mừng',
    templateCode: 'welcome-member',
    scope: 'tenant',
    audience: 'user',
    subject: 'Chào mừng {{name}} gia nhập hệ thống!',
    variables: 'name, loginUrl',
    textBody: 'Chào mừng {{name}} đã kích hoạt tài khoản thành công. Đăng nhập ngay tại: {{loginUrl}}',
    htmlBody: `<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 560px; margin: 0 auto; padding: 24px; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px;">
  <h2 style="color: #0369a1; margin-top: 0;">Xin chào {{name}}! 👋</h2>
  <p style="color: #334155; font-size: 15px; line-height: 1.6;">
    Tài khoản của bạn đã được khởi tạo thành công. Bạn hiện đã có thể truy cập đầy đủ các tiện ích trên nền tảng của chúng tôi.
  </p>
  <div style="text-align: center; margin: 28px 0;">
    <a href="{{loginUrl}}" style="background: #0ea5e9; color: white; padding: 12px 24px; border-radius: 8px; text-decoration: none; font-weight: 600; display: inline-block;">
      Đăng Nhập Vào Hệ Thống
    </a>
  </div>
  <p style="color: #64746d; font-size: 13px;">
    Hoặc truy cập đường link sau: <code>{{loginUrl}}</code>
  </p>
</div>`,
  },
];

export function TemplateList() {
  const auth = useAuth();
  const qc = useQueryClient();
  const [showCreate, setShowCreate] = useState(false);
  const [statusFilter, setStatusFilter] = useState('');
  const [scopeFilter, setScopeFilter] = useState('');
  const [audienceFilter, setAudienceFilter] = useState('');
  const [error, setError] = useState('');

  // Create form state
  const [formCode, setFormCode] = useState('');
  const [formScope, setFormScope] = useState<'tenant' | 'source'>('tenant');
  const [formAudience, setFormAudience] = useState<'user' | 'system'>('user');
  const [formSubject, setFormSubject] = useState('');
  const [formVars, setFormVars] = useState('');
  const [formTextBody, setFormTextBody] = useState('');
  const [formHtmlBody, setFormHtmlBody] = useState('');

  const applyPreset = (preset: TemplatePreset) => {
    setFormCode(preset.templateCode);
    setFormScope(preset.scope);
    setFormAudience(preset.audience);
    setFormSubject(preset.subject);
    setFormVars(preset.variables);
    setFormTextBody(preset.textBody);
    setFormHtmlBody(preset.htmlBody);
  };

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
      if (e instanceof ApiError) {
        setError(e.detailMessage ? `Lỗi (${e.code}): ${e.detailMessage}` : `Lỗi tạo template: ${e.code}`);
      } else {
        setError('Không thể tạo template. Vui lòng kiểm tra lại kết nối.');
      }
    },
  });

  return (
    <section>
      <header className="page-head">
        <div>
          <div className="eyebrow">NỘI DUNG & MẪU</div>
          <h1>Mẫu Thông Báo (Templates)</h1>
          <p>Quản lý mẫu nội dung email đa định dạng (Plain-text & HTML) kèm cơ chế phiên bản bất biến (Immutable Versioning).</p>
        </div>
        <button
          onClick={() => {
            setError('');
            applyPreset(PRESETS[0]);
            setShowCreate(true);
          }}
        >
          + Tạo Template Mới
        </button>
      </header>

      <div className="filters">
        <label>
          Trạng thái
          <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
            <option value="">Tất cả trạng thái</option>
            <option value="active">Đang hoạt động (active)</option>
            <option value="draft">Bản nháp (draft)</option>
            <option value="retired">Đã ngừng dùng (retired)</option>
          </select>
        </label>
        <label>
          Phạm vi (Scope)
          <select value={scopeFilter} onChange={(e) => setScopeFilter(e.target.value)}>
            <option value="">Tất cả phạm vi</option>
            <option value="tenant">Toàn bộ Tenant</option>
            <option value="source">Chỉ Device nguồn</option>
          </select>
        </label>
        <label>
          Đối tượng (Audience)
          <select value={audienceFilter} onChange={(e) => setAudienceFilter(e.target.value)}>
            <option value="">Tất cả đối tượng</option>
            <option value="user">User (Khách hàng)</option>
            <option value="system">System (Kỹ thuật / Nội bộ)</option>
          </select>
        </label>
      </div>

      {showCreate && (
        <div className="modal-backdrop">
          <div className="modal" style={{ maxWidth: '680px' }}>
            <div className="modal-head">
              <div>
                <h2>Tạo Mẫu Thông Báo Mới</h2>
                <p style={{ margin: '4px 0 0', fontSize: '0.85rem', color: 'var(--muted)' }}>
                  Chọn mẫu có sẵn để điền nhanh hoặc tự soạn thảo theo nhu cầu.
                </p>
              </div>
              <button className="ghost" onClick={() => setShowCreate(false)}>✕</button>
            </div>

            {/* Quick Presets Picker */}
            <div style={{ marginBottom: '16px' }}>
              <div style={{ fontSize: '0.8rem', fontWeight: 700, color: 'var(--muted)', marginBottom: '8px' }}>
                CHỌN MẪU GỢI Ý ĐIỀN NHANH:
              </div>
              <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                {PRESETS.map((p) => (
                  <button
                    key={p.id}
                    type="button"
                    className="ghost"
                    onClick={() => applyPreset(p)}
                    style={{
                      fontSize: '0.82rem',
                      padding: '6px 12px',
                      background: formCode === p.templateCode ? '#17221e' : 'white',
                      color: formCode === p.templateCode ? 'white' : 'inherit',
                      fontWeight: formCode === p.templateCode ? 700 : 500,
                    }}
                  >
                    {p.badge}
                  </button>
                ))}
              </div>
            </div>

            <form
              onSubmit={(e) => {
                e.preventDefault();
                const variables = formVars
                  .split(',')
                  .map((v) => v.trim())
                  .filter(Boolean);

                create.mutate({
                  templateCode: formCode.trim().toLowerCase(),
                  scope: formScope,
                  audience: formAudience,
                  subject: formSubject.trim(),
                  textBody: formTextBody.trim() || undefined,
                  htmlBody: formHtmlBody.trim() || undefined,
                  variables,
                });
              }}
            >
              <label>
                Mã Template (Template Code — dùng chữ thường và dấu gạch ngang)
                <input
                  value={formCode}
                  onChange={(e) => setFormCode(e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '-'))}
                  placeholder="vd: order-success, auth-otp, welcome-member"
                  required
                  maxLength={63}
                />
              </label>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                <label>
                  Phạm vi (Scope)
                  <select
                    value={formScope}
                    onChange={(e) => setFormScope(e.target.value as 'tenant' | 'source')}
                  >
                    <option value="tenant">Toàn bộ Tenant (Khuyên dùng)</option>
                    <option value="source">Chỉ Device nguồn cụ thể</option>
                  </select>
                </label>
                <label>
                  Đối tượng (Audience)
                  <select
                    value={formAudience}
                    onChange={(e) => setFormAudience(e.target.value as 'user' | 'system')}
                  >
                    <option value="user">User (Khách hàng)</option>
                    <option value="system">System (Kỹ thuật / Nội bộ)</option>
                  </select>
                </label>
              </div>

              <label>
                Tiêu đề email (Subject)
                <input
                  value={formSubject}
                  onChange={(e) => setFormSubject(e.target.value)}
                  placeholder="vd: Xác nhận đơn hàng #{{orderId}} thành công"
                  required
                />
              </label>

              <label>
                Danh sách biến số (Cách nhau bằng dấu phẩy)
                <input
                  value={formVars}
                  onChange={(e) => setFormVars(e.target.value)}
                  placeholder="vd: name, orderId, totalAmount"
                />
              </label>

              <label>
                Nội dung Plain-Text
                <textarea
                  value={formTextBody}
                  onChange={(e) => setFormTextBody(e.target.value)}
                  placeholder="Xin chào {{name}}, đơn hàng {{orderId}} của bạn đã thành công."
                  rows={3}
                />
              </label>

              <label>
                Nội dung HTML (Định dạng phong phú)
                <textarea
                  value={formHtmlBody}
                  onChange={(e) => setFormHtmlBody(e.target.value)}
                  placeholder="<p>Xin chào <strong>{{name}}</strong>...</p>"
                  rows={5}
                />
              </label>

              {error && <div className="error" role="alert">{error}</div>}

              <div className="modal-actions">
                <button type="button" className="ghost" onClick={() => setShowCreate(false)}>Hủy</button>
                <button disabled={create.isPending || !formCode.trim() || !formSubject.trim()}>
                  {create.isPending ? 'Đang tạo…' : 'Tạo Bản Nháp Template'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {q.isLoading ? (
        <p>Đang tải danh sách templates…</p>
      ) : q.error ? (
        <div className="error">Không tải được danh sách template. Vui lòng kiểm tra quyền truy cập.</div>
      ) : q.data?.items.length === 0 ? (
        <div className="empty">
          <h2>Chưa có Mẫu Thông Báo Nào</h2>
          <p>Tạo mẫu template đầu tiên để gửi thông báo động có biến số nội suy.</p>
          <button
            style={{ marginTop: '16px' }}
            onClick={() => {
              setError('');
              applyPreset(PRESETS[0]);
              setShowCreate(true);
            }}
          >
            + Tạo Mẫu Đầu Tiên Ngay
          </button>
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
                <th style={{ textAlign: 'right' }}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {q.data?.items.map((t) => (
                <tr key={t.id}>
                  <td>
                    <Link
                      to={`/templates/${t.id}`}
                      className="id"
                      style={{ fontWeight: 700, color: 'var(--primary, #0ea5e9)', textDecoration: 'none' }}
                    >
                      {t.templateCode}
                    </Link>
                    <small style={{ fontFamily: 'monospace' }}>{t.id.slice(0, 8)}…</small>
                  </td>
                  <td>
                    <strong>{t.subject}</strong>
                  </td>
                  <td>
                    <span
                      className="badge"
                      style={{
                        background: t.scope === 'tenant' ? '#e0f2fe' : '#f3e8ff',
                        color: t.scope === 'tenant' ? '#0369a1' : '#7e22ce',
                      }}
                    >
                      {t.scope}
                    </span>
                  </td>
                  <td>
                    <span
                      className="badge"
                      style={{
                        background: t.audience === 'user' ? '#dcfce7' : '#fef3c7',
                        color: t.audience === 'user' ? '#15803d' : '#b45309',
                      }}
                    >
                      {t.audience}
                    </span>
                  </td>
                  <td>
                    {t.variables && t.variables.length > 0 ? (
                      <code>{t.variables.join(', ')}</code>
                    ) : (
                      <span style={{ color: 'var(--muted)' }}>—</span>
                    )}
                  </td>
                  <td>
                    <Status value={t.status} />
                  </td>
                  <td><Time value={t.createdAt} /></td>
                  <td style={{ textAlign: 'right' }}>
                    <Link
                      to={`/templates/${t.id}`}
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
                      ⚙️ Chi tiết / Preview
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

export function TemplateDetail() {
  const { id } = useParams();
  const auth = useAuth();
  const nav = useNavigate();
  const qc = useQueryClient();

  const [activeTab, setActiveTab] = useState<'editor' | 'preview'>('preview');
  const [testVars, setTestVars] = useState<Record<string, string>>({});
  const [actionError, setActionError] = useState('');
  const [successNotice, setSuccessNotice] = useState('');

  // Direct test send modal state
  const [showSendModal, setShowSendModal] = useState(false);
  const [recipientEmail, setRecipientEmail] = useState('');
  const [testSendResult, setTestSendResult] = useState<{ success: boolean; message: string } | null>(null);

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
    onError: (e) => setActionError(e instanceof ApiError ? (e.detailMessage || e.code) : 'Lưu thất bại.'),
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
    onError: (e) => setActionError(e instanceof ApiError ? (e.detailMessage || e.code) : 'Không thể tạo bản sao phiên bản.'),
  });

  const publishMutation = useMutation({
    mutationFn: () =>
      auth.request<Template>(`/v1/templates/${id}/publish`, {
        method: 'POST',
      }),
    onSuccess: () => {
      setSuccessNotice('Đã xuất bản (Publish) phiên bản thành công! Mẫu đã có hiệu lực trên toàn hệ thống.');
      setTimeout(() => setSuccessNotice(''), 4000);
      q.refetch();
      qc.invalidateQueries({ queryKey: ['templates'] });
    },
    onError: (e) => setActionError(e instanceof ApiError ? (e.detailMessage || e.code) : 'Không thể xuất bản template.'),
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
    onError: (e) => setActionError(e instanceof ApiError ? (e.detailMessage || e.code) : 'Không thể retire template.'),
  });

  // Direct test dispatch via Admin token
  const testSendMutation = useMutation({
    mutationFn: (data: { recipientEmail: string; variables: Record<string, string> }) =>
      auth.request<{ id: string; status: string }>('/v1/notifications', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          channels: [
            {
              type: 'email',
              targets: [{ address: data.recipientEmail }],
            },
          ],
          content: {
            mode: 'template',
            templateCode: q.data?.templateCode,
            data: data.variables,
          },
        }),
      }),
    onSuccess: (res) => {
      setTestSendResult({
        success: true,
        message: `Gửi email thử nghiệm thành công! ID thông báo: ${res.id}. Vui lòng kiểm tra hộp thư đến.`,
      });
    },
    onError: (e) => {
      setTestSendResult({
        success: false,
        message: e instanceof ApiError ? `Lỗi gửi (${e.code}): ${e.detailMessage || 'Không thể gửi thử'}` : 'Gửi thử thất bại.',
      });
    },
  });

  if (q.isLoading) return <p>Đang tải chi tiết template…</p>;
  if (q.error || !q.data) return <div className="error">Không tìm thấy thông tin template.</div>;

  const t = q.data;

  // Fill mock sample data for all variables
  const fillSampleVariables = () => {
    const mock: Record<string, string> = {};
    t.variables?.forEach((v) => {
      if (v.toLowerCase().includes('name')) mock[v] = 'Nguyễn Văn A';
      else if (v.toLowerCase().includes('order')) mock[v] = 'DH-2026-999';
      else if (v.toLowerCase().includes('amount') || v.toLowerCase().includes('total')) mock[v] = '1.250.000 VNĐ';
      else if (v.toLowerCase().includes('otp') || v.toLowerCase().includes('code')) mock[v] = '686868';
      else if (v.toLowerCase().includes('minute') || v.toLowerCase().includes('expire')) mock[v] = '5';
      else if (v.toLowerCase().includes('url')) mock[v] = 'https://example.com/login';
      else mock[v] = `Giá trị mẫu cho ${v}`;
    });
    setTestVars(mock);
  };

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
      <button className="back ghost" onClick={() => nav('/templates')}>← Quay lại danh sách template</button>

      <header className="page-head">
        <div>
          <div className="eyebrow">CHI TIẾT MẪU THÔNG BÁO</div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <h1>{t.templateCode}</h1>
            <Status value={t.status} />
          </div>
          <p>Mã phiên bản: <code>{t.id}</code></p>
        </div>

        <div className="actions">
          {t.status === 'active' && (
            <button
              onClick={() => {
                setTestSendResult(null);
                setRecipientEmail('huong102145@st.vimaru.edu.vn');
                if (Object.keys(testVars).length === 0) fillSampleVariables();
                setShowSendModal(true);
              }}
              style={{ background: 'var(--green)' }}
            >
              ✉ Gửi Thư Thử Nghiệm
            </button>
          )}

          {t.status === 'draft' && (
            <button
              className="success"
              style={{ background: '#15803d', color: 'white' }}
              disabled={publishMutation.isPending}
              onClick={() => publishMutation.mutate()}
            >
              ✓ Xuất Bản (Publish)
            </button>
          )}

          {t.status === 'active' && (
            <>
              <button
                className="ghost"
                disabled={cloneMutation.isPending}
                onClick={() => cloneMutation.mutate()}
              >
                + Tạo Phiên Bản Nháp Mới
              </button>
              <button
                className="danger"
                disabled={retireMutation.isPending}
                onClick={() => {
                  if (confirm('Ngừng dùng (Retire) phiên bản template này? Mẫu sẽ không thể dùng cho notification mới.')) {
                    retireMutation.mutate();
                  }
                }}
              >
                Ngừng Dùng (Retire)
              </button>
            </>
          )}

          {t.status === 'retired' && (
            <button
              className="ghost"
              disabled={cloneMutation.isPending}
              onClick={() => cloneMutation.mutate()}
            >
              + Nhân Bản Thành Nháp Mới
            </button>
          )}
        </div>
      </header>

      {successNotice && <div className="success">{successNotice}</div>}
      {actionError && <div className="error">{actionError}</div>}

      {/* Direct Test Send Modal */}
      {showSendModal && (
        <div className="modal-backdrop">
          <div className="modal" style={{ maxWidth: '560px' }}>
            <div className="modal-head">
              <div>
                <h2>Gửi Thư Thử Nghiệm Với Template Này</h2>
                <p style={{ margin: '4px 0 0', fontSize: '0.85rem', color: 'var(--muted)' }}>
                  Hệ thống sẽ điền các biến số động và gửi email thật qua máy chủ Sender mặc định.
                </p>
              </div>
              <button className="ghost" onClick={() => setShowSendModal(false)}>✕</button>
            </div>

            <form
              onSubmit={(e) => {
                e.preventDefault();
                testSendMutation.mutate({
                  recipientEmail: recipientEmail.trim(),
                  variables: testVars,
                });
              }}
            >
              <label>
                Email người nhận thử nghiệm
                <input
                  type="email"
                  value={recipientEmail}
                  onChange={(e) => setRecipientEmail(e.target.value)}
                  placeholder="huong102145@st.vimaru.edu.vn"
                  required
                />
              </label>

              {/* Variable values for test send */}
              {t.variables && t.variables.length > 0 && (
                <div style={{ marginTop: '10px' }}>
                  <div style={{ fontSize: '0.78rem', fontWeight: 700, color: 'var(--muted)', marginBottom: '8px' }}>
                    DỮ LIỆU BIẾN SỐ SẼ NỘI SUY VÀO THƯ:
                  </div>
                  <div style={{ display: 'grid', gap: '8px' }}>
                    {t.variables.map((v) => (
                      <label key={v} style={{ margin: 0 }}>
                        <span style={{ fontSize: '0.8rem' }}>{`{{${v}}}`}:</span>
                        <input
                          value={testVars[v] || ''}
                          onChange={(e) => setTestVars((prev) => ({ ...prev, [v]: e.target.value }))}
                          placeholder={`Giá trị cho ${v}...`}
                          required
                        />
                      </label>
                    ))}
                  </div>
                </div>
              )}

              {testSendResult && (
                <div className={testSendResult.success ? 'success' : 'error'} style={{ marginTop: '14px' }}>
                  {testSendResult.message}
                </div>
              )}

              <div className="modal-actions" style={{ marginTop: '20px' }}>
                <button type="button" className="ghost" onClick={() => setShowSendModal(false)}>Đóng</button>
                <button disabled={testSendMutation.isPending || !recipientEmail.trim()}>
                  {testSendMutation.isPending ? 'Đang gửi thử…' : 'Gửi Thử Ngay'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      <div className="tabs">
        <button
          className={`tab ${activeTab === 'preview' ? 'active' : ''}`}
          onClick={() => setActiveTab('preview')}
        >
          👁️ Xem trước (Live Preview & Variables)
        </button>
        <button
          className={`tab ${activeTab === 'editor' ? 'active' : ''}`}
          onClick={() => setActiveTab('editor')}
        >
          ✏️ Trình chỉnh sửa nội dung
        </button>
      </div>

      {activeTab === 'preview' ? (
        <div className="grid">
          {/* Variables testing card */}
          <article className="card">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
              <h2 style={{ margin: 0 }}>Nhập biến thử nghiệm</h2>
              {t.variables && t.variables.length > 0 && (
                <button
                  type="button"
                  className="ghost"
                  style={{ fontSize: '0.75rem', padding: '4px 8px' }}
                  onClick={fillSampleVariables}
                >
                  ⚡ Điền Dữ Liệu Mẫu
                </button>
              )}
            </div>

            {t.variables && t.variables.length > 0 ? (
              <div style={{ display: 'grid', gap: '12px' }}>
                {t.variables.map((v) => (
                  <label key={v}>
                    Biến <code>{`{{${v}}}`}</code>:
                    <input
                      placeholder={`Nhập giá trị cho ${v}...`}
                      value={testVars[v] || ''}
                      onChange={(e) => setTestVars((prev) => ({ ...prev, [v]: e.target.value }))}
                    />
                  </label>
                ))}
              </div>
            ) : (
              <p style={{ color: 'var(--muted)', fontSize: '0.88rem' }}>Template này không khai báo biến số nào.</p>
            )}

            <div style={{ marginTop: '24px', paddingTop: '18px', borderTop: '1px solid var(--line)' }}>
              <h2>Thông tin phiên bản</h2>
              <dl>
                <dt>Trạng thái</dt>
                <dd><Status value={t.status} /></dd>
                <dt>Phạm vi</dt>
                <dd><span className="badge badge-muted">{t.scope}</span></dd>
                <dt>Đối tượng</dt>
                <dd><span className="badge badge-muted">{t.audience}</span></dd>
                <dt>Ngày tạo</dt>
                <dd><Time value={t.createdAt} /></dd>
                <dt>Cập nhật</dt>
                <dd><Time value={t.updatedAt} /></dd>
              </dl>
            </div>
          </article>

          {/* Render Preview card */}
          <article className="card">
            <h2>Kết quả kết xuất trực quan (Render Preview)</h2>

            <div style={{ marginBottom: '16px' }}>
              <div className="eyebrow">TIÊU ĐỀ EMAIL ĐÃ NỘI SUY (SUBJECT):</div>
              <p style={{ fontWeight: 700, fontSize: '1.15rem', margin: '4px 0 0 0', color: '#0f172a' }}>
                {renderText(t.subject)}
              </p>
            </div>

            {t.htmlBody ? (
              <div>
                <div className="eyebrow" style={{ marginBottom: '8px' }}>HTML PREVIEW (XEM TRƯỚC EMAIL THẬT):</div>
                <div className="preview-pane" style={{ background: '#f8fafc', padding: '12px' }}>
                  <iframe
                    title="html-preview"
                    srcDoc={renderText(t.htmlBody)}
                    sandbox="allow-same-origin"
                    style={{ width: '100%', height: '320px', border: '1px solid var(--line)', borderRadius: '8px', background: 'white' }}
                  />
                </div>
              </div>
            ) : null}

            {t.textBody ? (
              <div style={{ marginTop: '20px' }}>
                <div className="eyebrow" style={{ marginBottom: '8px' }}>PLAIN-TEXT PREVIEW:</div>
                <pre
                  style={{
                    background: '#f8faf9',
                    border: '1px solid var(--line)',
                    color: '#0f172a',
                    padding: '14px',
                    borderRadius: '8px',
                    whiteSpace: 'pre-wrap',
                    wordBreak: 'break-word',
                    fontSize: '0.88rem',
                    lineHeight: 1.5,
                  }}
                >
                  {renderText(t.textBody)}
                </pre>
              </div>
            ) : null}
          </article>
        </div>
      ) : (
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
                Nội dung HTML (Định dạng phong phú)
                <textarea
                  name="htmlBody"
                  defaultValue={t.htmlBody || ''}
                  disabled={t.status !== 'draft'}
                  rows={10}
                />
              </label>

              {t.status === 'draft' ? (
                <div className="modal-actions" style={{ marginTop: '18px' }}>
                  <button disabled={updateMutation.isPending}>Lưu Bản Nháp</button>
                </div>
              ) : (
                <p style={{ color: 'var(--muted)', fontSize: '0.85rem', marginTop: '14px', lineHeight: 1.4 }}>
                  💡 <em>Lưu ý:</em> Phiên bản đã xuất bản (active) là <strong>bất biến</strong> để bảo vệ tính toàn vẹn dữ liệu. Bấm nút <strong>+ Tạo phiên bản nháp mới</strong> ở góc trên để chỉnh sửa nội dung.
                </p>
              )}
            </form>
          </article>

          <article className="card">
            <h2>Thông tin phiên bản</h2>
            <dl>
              <dt>Trạng thái</dt>
              <dd><Status value={t.status} /></dd>

              <dt>Phạm vi (Scope)</dt>
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

              <dt>Cập nhật lần cuối</dt>
              <dd><Time value={t.updatedAt} /></dd>
            </dl>
          </article>
        </div>
      )}
    </section>
  );
}
