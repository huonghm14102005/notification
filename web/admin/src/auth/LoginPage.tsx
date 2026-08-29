import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Navigate, useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { useAuth, apiUrl } from './AuthContext';

const loginSchema = z.object({
  email: z.string().email('Email không hợp lệ').max(254),
  password: z.string().min(8, 'Mật khẩu có ít nhất 8 ký tự').max(128),
});
type LoginForm = z.infer<typeof loginSchema>;

const registerSchema = z.object({
  tenantName: z.string().min(2, 'Tên tổ chức tối thiểu 2 ký tự').max(200),
  tenantSlug: z
    .string()
    .min(3, 'Slug tối thiểu 3 ký tự')
    .max(63)
    .regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, 'Slug chỉ gồm chữ thường, số và dấu gạch ngang'),
  adminEmail: z.string().email('Email không hợp lệ').max(254),
  adminPassword: z.string().min(8, 'Mật khẩu tối thiểu 8 ký tự').max(128),
});
type RegisterForm = z.infer<typeof registerSchema>;

export function LoginPage() {
  const a = useAuth();
  const nav = useNavigate();
  const [isRegister, setIsRegister] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const loginForm = useForm<LoginForm>({ resolver: zodResolver(loginSchema) });
  const registerForm = useForm<RegisterForm>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      tenantName: 'Citad Organization',
      tenantSlug: 'citad-org',
      adminEmail: 'admin@citad.vn',
      adminPassword: '',
    },
  });

  if (a.ready && a.authenticated) return <Navigate to="/notifications" replace />;

  const onLogin = async (data: LoginForm) => {
    setError('');
    try {
      await a.login(data.email, data.password);
      nav('/notifications');
    } catch {
      setError('Email hoặc mật khẩu không đúng hoặc chưa được tạo.');
    }
  };

  const onRegister = async (data: RegisterForm) => {
    setError('');
    setSuccess('');
    try {
      const res = await fetch(apiUrl('/v1/tenants/register'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      });
      if (!res.ok) {
        let msg = 'Đăng ký thất bại';
        try {
          const body = await res.json();
          msg = body.error || body.code || msg;
          if (body.code === 'TENANT_SLUG_EXISTS') msg = 'Slug tổ chức này đã tồn tại';
          if (body.code === 'ADMIN_EMAIL_EXISTS') msg = 'Email quản trị viên này đã tồn tại';
        } catch {}
        throw new Error(msg);
      }
      setSuccess('Tạo tổ chức thành công! Đang tự động đăng nhập…');
      await a.login(data.adminEmail, data.adminPassword);
      nav('/notifications');
    } catch (e: any) {
      setError(e.message || 'Lỗi khi tạo tổ chức');
    }
  };

  return (
    <main className="login">
      <section>
        <div className="eyebrow">NOTIFICATION SERVER</div>
        <h1>
          Vận hành thông báo,
          <br />
          <i>không cần đọc log.</i>
        </h1>
        <p>Theo dõi delivery, xem nguyên nhân và xử lý sự cố từ một nơi.</p>
      </section>

      {!isRegister ? (
        <form onSubmit={loginForm.handleSubmit(onLogin)}>
          <h2>Đăng nhập</h2>
          <p>Dùng tài khoản quản trị tenant.</p>
          <label>
            Email
            <input autoFocus autoComplete="email" {...loginForm.register('email')} />
          </label>
          {loginForm.formState.errors.email && (
            <small role="alert">{loginForm.formState.errors.email.message}</small>
          )}
          <label>
            Mật khẩu
            <input
              type="password"
              autoComplete="current-password"
              {...loginForm.register('password')}
            />
          </label>
          {loginForm.formState.errors.password && (
            <small role="alert">{loginForm.formState.errors.password.message}</small>
          )}
          {error && (
            <div className="error" role="alert">
              {error}
            </div>
          )}
          <button disabled={loginForm.formState.isSubmitting}>
            {loginForm.formState.isSubmitting ? 'Đang đăng nhập…' : 'Đăng nhập'}
          </button>
          <div style={{ marginTop: '1rem', textAlign: 'center' }}>
            <button
              type="button"
              className="ghost"
              style={{ background: 'transparent', border: 'none', color: '#6ee7b7', cursor: 'pointer', textDecoration: 'underline' }}
              onClick={() => {
                setError('');
                setIsRegister(true);
              }}
            >
              Chưa có tài khoản? Đăng ký tổ chức mới
            </button>
          </div>
        </form>
      ) : (
        <form onSubmit={registerForm.handleSubmit(onRegister)}>
          <h2>Tạo tổ chức & Admin</h2>
          <p>Khởi tạo Tenant và tài khoản Quản trị viên đầu tiên.</p>
          <label>
            Tên tổ chức
            <input autoFocus placeholder="Ví dụ: Citad Organization" {...registerForm.register('tenantName')} />
          </label>
          {registerForm.formState.errors.tenantName && (
            <small role="alert">{registerForm.formState.errors.tenantName.message}</small>
          )}
          <label>
            Slug tổ chức
            <input placeholder="ví dụ: citad-org" {...registerForm.register('tenantSlug')} />
          </label>
          {registerForm.formState.errors.tenantSlug && (
            <small role="alert">{registerForm.formState.errors.tenantSlug.message}</small>
          )}
          <label>
            Email Admin
            <input autoComplete="email" placeholder="admin@citad.vn" {...registerForm.register('adminEmail')} />
          </label>
          {registerForm.formState.errors.adminEmail && (
            <small role="alert">{registerForm.formState.errors.adminEmail.message}</small>
          )}
          <label>
            Mật khẩu Admin
            <input
              type="password"
              autoComplete="new-password"
              placeholder="Tối thiểu 8 ký tự"
              {...registerForm.register('adminPassword')}
            />
          </label>
          {registerForm.formState.errors.adminPassword && (
            <small role="alert">{registerForm.formState.errors.adminPassword.message}</small>
          )}
          {error && (
            <div className="error" role="alert">
              {error}
            </div>
          )}
          {success && (
            <div style={{ color: '#34d399', fontSize: '0.875rem', marginBottom: '0.5rem' }}>
              {success}
            </div>
          )}
          <button disabled={registerForm.formState.isSubmitting}>
            {registerForm.formState.isSubmitting ? 'Đang tạo tổ chức…' : 'Đăng ký & Đăng nhập'}
          </button>
          <div style={{ marginTop: '1rem', textAlign: 'center' }}>
            <button
              type="button"
              className="ghost"
              style={{ background: 'transparent', border: 'none', color: '#9ca3af', cursor: 'pointer', textDecoration: 'underline' }}
              onClick={() => {
                setError('');
                setIsRegister(false);
              }}
            >
              ← Quay lại Đăng nhập
            </button>
          </div>
        </form>
      )}
    </main>
  );
}
