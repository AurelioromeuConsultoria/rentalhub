export function isPlatformAdminUser(usuario) {
  return Boolean(usuario?.isPlatformAdmin && usuario?.isRootTenant);
}
