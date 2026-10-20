<?php
// Secrets live in config.php (git-ignored). Copy config.example.php -> config.php on the server.
$config = file_exists(__DIR__ . '/config.php') ? require __DIR__ . '/config.php' : [];
$token = $config['bot_token'] ?? '';
if ($token === '') {
    die('Missing config: copy config.example.php to config.php and set bot_token');
$webhookUrl = $config['webhook_url'] ?? "https://telbot.hekmateislami.com/webhook.php";
$url = "https://api.telegram.org/bot{$token}/setWebhook?url=" . urlencode($webhookUrl);

// Optional: shared secret token. When set, Telegram attaches the header
// X-Telegram-Bot-Api-Secret-Token to every webhook delivery, which the
// backend uses to reject forged payloads.
$secret = $config['webhook_secret'] ?? '';
if ($secret !== '') {
    $url .= '&secret_token=' . urlencode($secret);
}

$ch = curl_init($url);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
curl_setopt($ch, CURLOPT_TIMEOUT, 15);
$response = curl_exec($ch);
$errno = curl_errno($ch);
curl_close($ch);

if ($errno !== 0) {
    http_response_code(502);
    echo "cURL error: " . htmlspecialchars($errno) . " (is outbound HTTPS allowed on this host?)";
} else {
    echo "Telegram Response: <br>" . htmlspecialchars($response);
}
