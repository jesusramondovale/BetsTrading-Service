export default {
  async fetch(request) {
    const url = new URL(request.url);

    if (url.pathname === '/app-ads.txt') {
      return fetch('https://api.betstrading.online/api/Info/AddAps', {
        method: 'GET',
        headers: { 'accept': 'text/plain' }
      });
    }

    // fallback normal
    return fetch(request);
  }
}
