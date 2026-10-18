import {
  browserApi as api,
  csrfHeaders,
  failure,
  guarded,
  type ApiResult,
} from "./browser";
import type { components } from "./schema";

/**
 * SPEC-0017 — chamadas do navegador à API do painel do admin (cidades e estilos), pela base comum de `./browser`
 * (mesmo domínio, cookie de sessão HttpOnly, `X-CSRF` em toda mutação).
 */
export type AdminCity = components["schemas"]["AdminCityResponse"];

export type Style = components["schemas"]["StyleResponse"];

export type CityInput = components["schemas"]["CreateCityRequest"];

export type CityUpdate = components["schemas"]["UpdateCityRequest"];

/** `POST /api/v1/admin/cities`: cria a cidade (sem `slug`, ele é gerado do nome). */
export function createCity(input: CityInput): Promise<ApiResult<AdminCity>> {
  return guarded(async () => {
    const { data, response, error } = await api.POST("/api/v1/admin/cities", {
      body: input,
      headers: await csrfHeaders(),
    });
    return data ? { ok: true, value: data } : failure(response.status, error);
  });
}

/** `PUT /api/v1/admin/cities/{slug}`: troca nome, UF e fuso (o slug não muda). */
export function updateCity(
  slug: string,
  input: CityUpdate,
): Promise<ApiResult<AdminCity>> {
  return guarded(async () => {
    const { data, response, error } = await api.PUT(
      "/api/v1/admin/cities/{slug}",
      {
        params: { path: { slug } },
        body: input,
        headers: await csrfHeaders(),
      },
    );
    return data ? { ok: true, value: data } : failure(response.status, error);
  });
}

/** `POST /api/v1/admin/styles`: estilo novo (slug gerado do nome). */
export function createStyle(name: string): Promise<ApiResult<Style>> {
  return guarded(async () => {
    const { data, response, error } = await api.POST("/api/v1/admin/styles", {
      body: { name },
      headers: await csrfHeaders(),
    });
    return data ? { ok: true, value: data } : failure(response.status, error);
  });
}

/** `PUT /api/v1/admin/styles/{slug}`: renomeia o estilo (o slug não muda). */
export function renameStyle(
  slug: string,
  name: string,
): Promise<ApiResult<Style>> {
  return guarded(async () => {
    const { data, response, error } = await api.PUT(
      "/api/v1/admin/styles/{slug}",
      {
        params: { path: { slug } },
        body: { name },
        headers: await csrfHeaders(),
      },
    );
    return data ? { ok: true, value: data } : failure(response.status, error);
  });
}
