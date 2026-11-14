const { env } = require('process');

const target = env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` :
  env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'http://localhost:5209';

const PROXY_CONFIG = {
  '/app-config.json': {
    target,
    secure: false,
    changeOrigin: false
  },
  '/api': {
    target: target,
    secure: false
  },
  '/scalar': {
    target: target,
    secure: false
  },
  '/uploads': {
    target: target,
    secure: false
  },
  '/mvc': {
    target: target,
    secure: false
  },
  '/realtimeHub/negotiate': {
    target: target,
    secure: false
  },
  '/realtimeHub': {
    target: target,
    secure: false,
    ws: true
  }
};

module.exports = PROXY_CONFIG;

