import React from 'react';
import ReactDOM from 'react-dom/client';
import './index.css';
import './i18n';
import AppRoutes from './routes/AppRoutes';
import reportWebVitals from './reportWebVitals';

import TokenMonitor from './routes/TokenMonitor';
import NotificationPopup from './components/UI/NotificationPopup';
import { NotificationProvider } from './features/notifications/NotificationProvider';

const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(
  <React.StrictMode>
    <TokenMonitor />
    <NotificationPopup />
    <NotificationProvider>
      <AppRoutes />
    </NotificationProvider>
  </React.StrictMode>
);

// If you want to start measuring performance in your app, pass a function
// to log results (for example: reportWebVitals(console.log))
// or send to an analytics endpoint. Learn more: https://bit.ly/CRA-vitals
reportWebVitals();
