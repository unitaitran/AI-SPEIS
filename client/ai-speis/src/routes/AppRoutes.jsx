import React, { useEffect, useState } from 'react';
import App from '../App';
import AdminRoutes from './AdminRoutes';
import { NAVIGATION_EVENT } from './navigation';

function AppRoutes() {
  const [pathname, setPathname] = useState(window.location.pathname);

  useEffect(() => {
    const syncPathname = () => setPathname(window.location.pathname);

    window.addEventListener('popstate', syncPathname);
    window.addEventListener(NAVIGATION_EVENT, syncPathname);

    return () => {
      window.removeEventListener('popstate', syncPathname);
      window.removeEventListener(NAVIGATION_EVENT, syncPathname);
    };
  }, []);

  if (pathname === '/admin' || pathname.startsWith('/admin/')) {
    return <AdminRoutes pathname={pathname} />;
  }

  return <App />;
}

export default AppRoutes;
