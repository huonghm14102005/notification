import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ApiError, type Detail } from '../shared/types';
import { ConfirmDialog } from './ConfirmDialog';
import { Status, Time } from './Status';

export function NotificationDetail() {
  const { id = '' } = useParams();
  const auth = useAuth();
  const nav = useNavigate();
  const client = useQueryClient();
  const [action, setAction] = useState<'retry' | 'cancel'>();

  const q = useQuery({
    queryKey: ['notification', id],
    queryFn: () => auth.request<Detail>(`/v1/notifications/${id}`),
  });

  const mutation = useMutation({
    mutationFn: async (type: 'retry' | 'cancel') =>
      auth.request<{ id: string }>(`/v1/notifications/${id}/${type}`, { method: 'POST' }),
    onSuccess: async (x, type) => {
      setAction(undefined);
      await client.invalidateQueries({ queryKey: ['notifications'] });
      if (type === 'retry' && x?.id) nav(`/notifications/${x.id}`);
      else q.refetch();
    },
  });

  if (q.isPending) return <div className="skeleton" aria-label="Đang tải chi tiết" />;
  if (q.isError)
    return (
      <div className="empty">
        <h1>Không tải được thông báo</h1>
        <Link to="/notifications">Quay lại danh sách</Link>
      </div>
    );

  const x = q.data;
  const conflict = mutation.error instanceof ApiError && mutation.error.status === 409;

  return (
    <>
      <Link className="back" to="/notifications">
        ← Quay lại danh sách
      </Link>
      <header className="detail-head">
        <div>
          <div className="eyebrow">NOTIFICATION RECORD</div>
          <h1>
            {x.id.slice(0, 8)}
            <small>{x.id}</small>
          </h1>
          <Status value={x.status} />
        </div>
        <div className="actions">
          {['failed', 'partially_delivered'].includes(x.status) && (
            <button onClick={() => setAction('retry')}>Gửi lại phần lỗi</button>
          )}
          {x.status === 'accepted' && (
            <button className="danger" onClick={() => setAction('cancel')}>
              Hủy thông báo
            </button>
          )}
        </div>
      </header>

      {conflict && (
        <div className="error" role="alert">
          Trạng thái đã thay đổi. Hãy tải lại trang để xem dữ liệu mới nhất.
        </div>
      )}

      <section className="grid">
        <article className="card">
          <h2>Thông tin tổng quan</h2>
          <dl>
            <dt>Nguồn gửi (Producer)</dt>
            <dd>{x.producerName}</dd>
            <dt>Sender Key</dt>
            <dd><code>{x.senderKey || 'default'}</code></dd>
            <dt>Địa chỉ người nhận</dt>
            <dd><code>{x.recipientEmail}</code></dd>
            {x.recipientRef && (
              <>
                <dt>Mã tham chiếu (Ref)</dt>
                <dd><code>{x.recipientRef}</code></dd>
              </>
            )}
            <dt>Thời gian tạo</dt>
            <dd><Time value={x.createdAt} /></dd>
            <dt>Cập nhật lần cuối</dt>
            <dd><Time value={x.updatedAt} /></dd>
            {x.sentAt && (
              <>
                <dt>Hoàn tất lúc</dt>
                <dd><Time value={x.sentAt} /></dd>
              </>
            )}
            {x.failureReason && (
              <>
                <dt>Lý do thất bại</dt>
                <dd style={{ color: 'var(--color-danger, #ef4444)' }}>{x.failureReason}</dd>
              </>
            )}
          </dl>
        </article>

        <article className="card">
          <h2>Nội dung thông báo (Đã giải mã)</h2>
          <div style={{ marginBottom: '12px' }}>
            <span className="eyebrow" style={{ display: 'block', marginBottom: '4px' }}>Tiêu đề:</span>
            <h3 style={{ margin: 0 }}>{x.subject ?? '(Không có tiêu đề)'}</h3>
          </div>
          <span className="eyebrow" style={{ display: 'block', marginBottom: '4px' }}>Nội dung:</span>
          <pre
            style={{
              whiteSpace: 'pre-wrap',
              wordBreak: 'break-word',
              background: 'var(--bg-subtle, #1e293b)',
              padding: '12px',
              borderRadius: '6px',
              fontSize: '0.9rem',
            }}
          >
            {x.body ?? 'Nội dung không khả dụng.'}
          </pre>
        </article>
      </section>

      <section className="timeline" style={{ marginTop: '24px' }}>
        <h2>Nhật ký các lần gửi (Delivery Attempts)</h2>
        {x.deliveryAttempts.length === 0 ? (
          <p style={{ color: 'var(--muted)' }}>Chưa có attempt nào được thực thi.</p>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            {x.deliveryAttempts.map((at) => (
              <article
                key={at.attemptNo}
                className="card"
                style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}
              >
                <div>
                  <div style={{ display: 'flex', gap: '8px', alignItems: 'center', marginBottom: '6px' }}>
                    <strong>Lần thử #{at.attemptNo}</strong>
                    <Status value={at.result} />
                    {at.providerMessageId && (
                      <small style={{ color: 'var(--muted)' }}>ID: {at.providerMessageId}</small>
                    )}
                  </div>
                  {at.errorCode && (
                    <p style={{ margin: 0, color: 'var(--color-danger, #ef4444)', fontSize: '0.85rem' }}>
                      <strong>{at.errorCode}:</strong> {at.errorMessage}
                    </p>
                  )}
                </div>
                <div style={{ textAlign: 'right', fontSize: '0.85rem', color: 'var(--muted)' }}>
                  <div>Bắt đầu: <Time value={at.startedAt} /></div>
                  <div>Kết thúc: <Time value={at.finishedAt} /></div>
                </div>
              </article>
            ))}
          </div>
        )}
      </section>

      <ConfirmDialog
        open={!!action}
        busy={mutation.isPending}
        onCancel={() => setAction(undefined)}
        onConfirm={() => action && mutation.mutate(action)}
        title={action === 'retry' ? 'Gửi lại phần thất bại?' : 'Hủy thông báo?'}
      >
        {action === 'retry'
          ? 'Hệ thống sẽ tạo thông báo mới và chỉ gửi lại delivery thất bại. Delivery đã thành công sẽ không bị gửi lại.'
          : 'Chỉ các thông báo chưa có attempt xử lý mới có thể hủy. Thao tác này không thể hoàn tác.'}
      </ConfirmDialog>
    </>
  );
}
