import { HttpTransportType, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import type { HubConnectionFactory } from './types.js';

/** Reconnect delays after a live connection drops; also used for the first connection attempts. */
export const reconnectDelaysMs: readonly number[] = [0, 2000, 5000, 10000, 30000];

/**
 * WebSockets without negotiation: the connection is a single request, so it can land on any evaluation pod and needs
 * no sticky sessions. The SDK key travels in the `access_token` query parameter because browsers cannot set headers
 * on WebSocket requests.
 */
export const createHubConnection: HubConnectionFactory = (url, accessTokenFactory) =>
  new HubConnectionBuilder()
    .withUrl(url, {
      accessTokenFactory,
      transport: HttpTransportType.WebSockets,
      skipNegotiation: true,
    })
    .withAutomaticReconnect([...reconnectDelaysMs])
    .configureLogging(LogLevel.None)
    .build();
