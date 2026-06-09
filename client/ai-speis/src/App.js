import './styles/reset.css';
import './styles/variables.css';
import './styles/globals.css';
import AdminLayout from './layouts/admin/AdminLayout';
import AdminDashboardPage from './pages/admin/Dashboard/AdminDashboardPage';

function App() {
  return (
    <AdminLayout>
      <AdminDashboardPage />
    </AdminLayout>
  );
}

export default App;
