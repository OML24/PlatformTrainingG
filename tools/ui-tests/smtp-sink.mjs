// Minimal SMTP sink for isolated UI tests. It accepts mail without relaying it
// and writes metadata plus message bodies to a path outside the repository.
import net from 'node:net';
import { appendFileSync, mkdirSync } from 'node:fs';
import { dirname } from 'node:path';

const host = process.env.ZU_SMTP_SINK_HOST ?? '127.0.0.1';
const port = Number(process.env.ZU_SMTP_SINK_PORT ?? '2525');
const output = process.env.ZU_SMTP_SINK_OUTPUT;
if (!output) throw new Error('ZU_SMTP_SINK_OUTPUT is required.');
if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error('Invalid SMTP sink port.');
mkdirSync(dirname(output), { recursive: true });
let uncertainOnce = process.env.ZU_SMTP_SINK_UNCERTAIN_ONCE === 'true';

const server = net.createServer(socket => {
  socket.setEncoding('utf8');
  socket.on('error', error => {
    if (error.code !== 'ECONNRESET' && error.code !== 'EPIPE')
      console.error(JSON.stringify({ status: 'client-error', code: error.code ?? 'UNKNOWN' }));
  });
  socket.write('220 localhost ZU LMS isolated test sink\r\n');
  let buffer = '', dataMode = false, body = [], sender = '', recipients = [];
  const reply = text => socket.write(`${text}\r\n`);
  const reset = () => { dataMode = false; body = []; sender = ''; recipients = []; };
  socket.on('data', chunk => {
    buffer += chunk;
    let split;
    while ((split = buffer.indexOf('\n')) >= 0) {
      const line = buffer.slice(0, split).replace(/\r$/, '');
      buffer = buffer.slice(split + 1);
      if (dataMode) {
        if (line === '.') {
          appendFileSync(output, JSON.stringify({ at: new Date().toISOString(), sender,
            recipients, message: body.join('\r\n') }) + '\n', 'utf8');
          reset();
          if (uncertainOnce) { uncertainOnce = false; socket.destroy(); }
          else reply('250 2.0.0 accepted by isolated sink');
        } else body.push(line.startsWith('..') ? line.slice(1) : line);
        continue;
      }
      const command = line.split(/\s+/, 1)[0].toUpperCase();
      if (command === 'EHLO') reply('250-localhost\r\n250-8BITMIME\r\n250 SIZE 10485760');
      else if (command === 'HELO') reply('250 localhost');
      else if (command === 'MAIL') { sender = line.slice(5); reply('250 2.1.0 ok'); }
      else if (command === 'RCPT') { recipients.push(line.slice(5)); reply('250 2.1.5 ok'); }
      else if (command === 'DATA') { dataMode = true; body = []; reply('354 end with <CRLF>.<CRLF>'); }
      else if (command === 'RSET') { reset(); reply('250 2.0.0 reset'); }
      else if (command === 'NOOP') reply('250 2.0.0 ok');
      else if (command === 'QUIT') { reply('221 2.0.0 bye'); socket.end(); }
      else reply('502 5.5.2 unsupported command');
    }
  });
});
server.listen(port, host, () => console.log(JSON.stringify({ status: 'ready', host, port, output })));
