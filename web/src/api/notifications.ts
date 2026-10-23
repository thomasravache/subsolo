/**
 * SPEC-0030 — chamadas do navegador à API de avisos (§6). Esqueleto do G1: só o tipo das preferências
 * (`GET/PUT /api/v1/me/notification-preferences`); as chamadas entram no G2, pela base comum de `./browser`, com o tipo
 * vindo do cliente gerado (`components["schemas"]`).
 */
export interface NotificationPreferences {
  newShowPush: boolean;
  dailyDigestEmail: boolean;
  reminderPush: boolean;
  reminderEmail: boolean;
  pushDevices: number;
}
