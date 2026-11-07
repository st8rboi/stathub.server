// Stathub Telegram bot: открывает mini app статхаб.рф в полноэкранном режиме.
// Без зависимостей, Node 18+.
//
// Работает через вебхук и отвечает прямо в теле ответа на него (Bot API это допускает).
// Поэтому серверу не нужно самому ходить в api.telegram.org: из РФ он недоступен.
// Токен на сервере не нужен — только для разовой настройки с компьютера, где Telegram доступен:
//
//   BOT_TOKEN=... WEBHOOK_SECRET=... node bot.mjs setup   # вебхук, кнопка меню, команды
//   BOT_TOKEN=... node bot.mjs info                       # состояние вебхука и последняя ошибка
//
// Без аргументов — HTTP-сервер для вебхука (в docker compose, за nginx).

import { createServer } from 'node:http';
import { timingSafeEqual } from 'node:crypto';

// IDN-домен в punycode: статхаб.рф
const SITE = 'https://xn--80aad5dfbv.xn--p1ai';
const WEBAPP_URL = process.env.WEBAPP_URL ?? `${SITE}/`;
const WEBHOOK_URL = process.env.WEBHOOK_URL ?? `${SITE}/tg/webhook`;
const WEBHOOK_SECRET = process.env.WEBHOOK_SECRET ?? '';
const BOT_USERNAME = (process.env.BOT_USERNAME ?? '').replace(/^@/, '');
const PORT = Number(process.env.PORT ?? 8080);

const TEXT = 'Статхаб — турниры, матчи и статистика любительского футбола.';
const BUTTON_TEXT = 'Открыть Статхаб';

// ---------- разовая настройка (запускается локально) ----------

async function call(method, params = {}) {
  const token = process.env.BOT_TOKEN;
  if (!token) throw new Error('BOT_TOKEN не задан');
  const res = await fetch(`https://api.telegram.org/bot${token}/${method}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(params),
  });
  const data = await res.json();
  if (!data.ok) throw new Error(`${method}: ${data.description}`);
  return data.result;
}

async function setup() {
  if (!/^[A-Za-z0-9_-]{16,256}$/.test(WEBHOOK_SECRET)) {
    throw new Error('WEBHOOK_SECRET: 16–256 символов из A-Z a-z 0-9 _ - (например, openssl rand -hex 32)');
  }
  const me = await call('getMe');
  await call('setWebhook', {
    url: WEBHOOK_URL,
    secret_token: WEBHOOK_SECRET,
    allowed_updates: ['message'],
    drop_pending_updates: true,
  });
  await call('setChatMenuButton', {
    menu_button: { type: 'web_app', text: 'Статхаб', web_app: { url: WEBAPP_URL } },
  });
  await call('setMyCommands', { commands: [{ command: 'start', description: 'Открыть Статхаб' }] });
  console.log(`@${me.username}: вебхук ${WEBHOOK_URL}, кнопка меню ${WEBAPP_URL}`);
  console.log(`На сервере в .env: BOT_USERNAME=${me.username}`);
}

async function info() {
  const i = await call('getWebhookInfo');
  console.log(JSON.stringify(i, null, 2));
  if (i.last_error_message) {
    console.log(`\nПоследняя ошибка (${new Date(i.last_error_date * 1000).toISOString()}): ${i.last_error_message}`);
  }
}

// ---------- вебхук (на сервере) ----------

function replyMarkup() {
  // Прямая ссылка на Main Mini App с mode=fullscreen открывает приложение сразу на весь экран
  // (нужны Main Mini App и Launch Mode = Fullscreen в BotFather). Без имени бота — обычная web_app-кнопка.
  const button = BOT_USERNAME
    ? { text: BUTTON_TEXT, url: `https://t.me/${BOT_USERNAME}?startapp&mode=fullscreen` }
    : { text: BUTTON_TEXT, web_app: { url: WEBAPP_URL } };
  return { inline_keyboard: [[button]] };
}

function secretOk(header) {
  if (!WEBHOOK_SECRET || typeof header !== 'string') return false;
  const a = Buffer.from(header);
  const b = Buffer.from(WEBHOOK_SECRET);
  return a.length === b.length && timingSafeEqual(a, b);
}

function serve() {
  if (!WEBHOOK_SECRET) console.error('WEBHOOK_SECRET не задан — все запросы вебхука отклоняются');
  if (!BOT_USERNAME) console.warn('BOT_USERNAME не задан — кнопка откроет mini app не на весь экран');

  const server = createServer((req, res) => {
    if (req.method === 'GET' && req.url === '/health') {
      res.writeHead(200).end('ok');
      return;
    }
    if (req.method !== 'POST') {
      res.writeHead(405).end();
      return;
    }
    if (!secretOk(req.headers['x-telegram-bot-api-secret-token'])) {
      res.writeHead(403).end();
      return;
    }

    let body = '';
    req.setEncoding('utf8');
    req.on('data', (chunk) => {
      body += chunk;
      if (body.length > 1_000_000) req.destroy();
    });
    req.on('end', () => {
      let reply = null;
      try {
        const msg = JSON.parse(body).message;
        if (msg?.chat?.type === 'private') {
          console.log(`message from ${msg.from?.id}: ${msg.text ?? '<non-text>'}`);
          reply = { method: 'sendMessage', chat_id: msg.chat.id, text: TEXT, reply_markup: replyMarkup() };
        }
      } catch (e) {
        console.error('bad update:', e.message);
      }
      // Всегда 200: иначе Telegram будет повторять апдейт.
      if (reply) {
        res.writeHead(200, { 'Content-Type': 'application/json' }).end(JSON.stringify(reply));
      } else {
        res.writeHead(200).end();
      }
    });
  });

  server.listen(PORT, () => console.log(`webhook listening on :${PORT}`));
  for (const sig of ['SIGINT', 'SIGTERM']) process.on(sig, () => server.close(() => process.exit(0)));
}

// ---------- точка входа ----------

const cmd = process.argv[2];
try {
  if (cmd === 'setup') await setup();
  else if (cmd === 'info') await info();
  else serve();
} catch (e) {
  console.error(e.message);
  process.exit(1);
}
