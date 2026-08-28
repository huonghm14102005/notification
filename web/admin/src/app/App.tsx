import { Navigate, NavLink, Outlet, Route, Routes, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { LoginPage } from '../auth/LoginPage';
import { NotificationList } from '../notifications/NotificationList';
import { NotificationDetail } from '../notifications/NotificationDetail';
import { Profile, UserDetail, UserList } from '../users/Users';
import { DeviceList, DeviceDetail } from '../devices/Devices';
import { SenderList, SenderDetail } from '../senders/Senders';
import { TemplateList, TemplateDetail } from '../templates/Templates';

function Guard() {
  const auth = useAuth();
  if (!auth.ready) {
    return (
      <main className="center">
        <div className="spinner" aria-label="Đang tải" />
      </main>
    );
  }
  return auth.authenticated ? <Outlet /> : <Navigate to="/login" replace />;
}

function Shell() {
  const auth = useAuth();
  const nav = useNavigate();

  return (
    <div className="shell">
      <aside>
        <div className="brand">
          <span>NT</span>
          <div>
            Notification
            <small>Operations console</small>
          </div>
        </div>

        <nav aria-label="Điều hướng chính">
          <NavLink to="/notifications">Thông báo</NavLink>
          <NavLink to="/devices">Thiết bị & Keys</NavLink>
          <NavLink to="/templates">Mẫu thông báo</NavLink>
          <NavLink to="/senders">Cấu hình SMTP</NavLink>
          <NavLink to="/users">Người dùng</NavLink>
          <NavLink to="/profile">Hồ sơ</NavLink>
        </nav>

        <button
          className="ghost logout"
          onClick={async () => {
            await auth.logout();
            nav('/login');
          }}
        >
          Đăng xuất
        </button>
      </aside>

      <main>
        <Outlet />
      </main>
    </div>
  );
}

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<Guard />}>
        <Route element={<Shell />}>
          <Route path="/notifications" element={<NotificationList />} />
          <Route path="/notifications/:id" element={<NotificationDetail />} />
          <Route path="/devices" element={<DeviceList />} />
          <Route path="/devices/:id" element={<DeviceDetail />} />
          <Route path="/templates" element={<TemplateList />} />
          <Route path="/templates/:id" element={<TemplateDetail />} />
          <Route path="/senders" element={<SenderList />} />
          <Route path="/senders/:id" element={<SenderDetail />} />
          <Route path="/users" element={<UserList />} />
          <Route path="/users/:id" element={<UserDetail />} />
          <Route path="/profile" element={<Profile />} />
          <Route index element={<Navigate to="/notifications" replace />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
