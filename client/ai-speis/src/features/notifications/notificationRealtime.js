import { HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr';
import { ENDPOINTS } from '../../config/api';

export function createNotificationRealtimeConnection({ onCreated, onRead, onReadAll, onArchived, onReconnected }) {
  const connection = new HubConnectionBuilder()
    .withUrl(ENDPOINTS.NOTIFICATION_HUB, {
      accessTokenFactory: () => localStorage.getItem('token') || '',
      transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
      // Authentication is supplied by the bearer token above. Keeping cookies off
      // avoids a CORS credentials rejection when the React app and API use different origins.
      withCredentials: false,
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(LogLevel.Warning)
    .build();

  connection.on('notification.created', onCreated);
  connection.on('notification.read', onRead);
  connection.on('notification.read-all', onReadAll);
  connection.on('notification.archived', onArchived);
  connection.onreconnected(onReconnected);

  return connection;
}
