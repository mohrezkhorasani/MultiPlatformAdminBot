<?php
// config.example.php
// Copy this file to config.php and fill in real values.
// config.php is git-ignored and must NOT be committed (it holds live secrets).

return [
    // Telegram bot token (required on the server that receives the webhook)
    'bot_token'      => 'PUT_YOUR_TELEGRAM_BOT_TOKEN_HERE',

    // .NET backend that receives forwarded webhook payloads
    'dotnet_url'     => 'https://your-backend.example.com/api/webhook/telegram',

    // Telegram webhook URL pointing to webhook.php on your server
    'webhook_url'    => 'https://your-domain.com/webhook.php',

    // Shared proxy secret (must match TelegramProxy:Secret / PROXY_SECRET in the .NET backend)
    'proxy_secret'   => 'PUT_YOUR_PROXY_SECRET_HERE',

    // Shared webhook secret. Set a long random string here AND in the .NET
    // app's Webhook:SecretToken setting, then re-run set_webhook.php.
    // Telegram then attaches X-Telegram-Bot-Api-Secret-Token to every
    // delivery and the backend rejects any payload without the right token.
    'webhook_secret' => 'PUT_A_LONG_RANDOM_STRING_HERE',

    // Chat id that receives the raw payload for debugging (null to disable)
    'debug_chat_id'  => null,
];
