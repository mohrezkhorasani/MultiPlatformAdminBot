<?php
// webhook.php
// دریافت Webhook تلگرام، ارسال پیام "صبر کنید" به کاربر، سپس فوروارد به ASP.NET Core

// ==== تنظیمات ====
// Secrets live in config.php (git-ignored). Copy config.example.php -> config.php on the server.
$config        = file_exists(__DIR__ . '/config.php') ? require __DIR__ . '/config.php' : [];
$bot_token     = $config['bot_token'] ?? '';
$dotnet_url    = $config['dotnet_url'] ?? '';
$wait_text     = 'لطفاً کمی صبر کنید... ⏳';
$debug_chat_id = $config['debug_chat_id'] ?? null; // چت آیدی که کل بادی و مقادیر دریافتشده به آن ارسال میشود

if ($bot_token === '') {
    http_response_code(500);
    die('Missing config: copy config.example.php to config.php and set bot_token');
}

// ==== خواندن payload ====
$payload = file_get_contents('php://input');
if (empty($payload)) {
    http_response_code(200);
    die('No payload');
}

// ==== تابع ارسال پیام به تلگرام ====
function sendTelegramMessage($token, $chat_id, $text, $parse_mode = null) {
    $url = "https://api.telegram.org/bot{$token}/sendMessage";
    $post = [
        'chat_id'                  => $chat_id,
        'text'                     => $text,
        'disable_web_page_preview' => true,
    ];
    if ($parse_mode) {
        $post['parse_mode'] = $parse_mode;
    }

    $ch = curl_init($url);
    curl_setopt($ch, CURLOPT_POST, true);
    curl_setopt($ch, CURLOPT_POSTFIELDS, http_build_query($post));
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    curl_setopt($ch, CURLOPT_TIMEOUT, 8);
    curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, true);

    $result = curl_exec($ch);
    curl_close($ch);
    return $result;
}

// ==== تابع escape برای حالت HTML تلگرام ====
function tgEscape($text) {
    return htmlspecialchars($text, ENT_QUOTES | ENT_SUBSTITUTE | ENT_HTML5, 'UTF-8');
}

// ==== تابع ارسال متن طولانی (تکهتکه با حفظ تگها) ====
function sendLongTelegramMessage($token, $chat_id, $text, $parse_mode = null) {
    $limit = 4000;
    if (mb_strlen($text) <= $limit) {
        sendTelegramMessage($token, $chat_id, $text, $parse_mode);
        return;
    }
    // تقسیم بر اساس خطوط جدید برای حفظ ساختار
    $lines = explode("\n", $text);
    $chunk = '';
    foreach ($lines as $line) {
        if (mb_strlen($chunk) + mb_strlen($line) + 1 > $limit) {
            sendTelegramMessage($token, $chat_id, $chunk, $parse_mode);
            $chunk = '';
        }
        $chunk .= $line . "\n";
    }
    if (trim($chunk) !== '') {
        sendTelegramMessage($token, $chat_id, $chunk, $parse_mode);
    }
}

// ==== ۰) ارسال کل بادی و مقادیر دریافتشده به چت آیدی دیباگ ====

// جمعآوری هدرها
$headers = [];
if (function_exists('getallheaders')) {
    $headers = getallheaders();
} else {
    foreach ($_SERVER as $name => $value) {
        if (substr($name, 0, 5) == 'HTTP_') {
            $headers[str_replace(' ', '-', ucwords(strtolower(str_replace('_', ' ', substr($name, 5)))))] = $value;
        }
    }
}

// مرتبسازی JSON برای نمایش زیبا
$decoded = json_decode($payload, true);
if (json_last_error() === JSON_ERROR_NONE) {
    $pretty_body = json_encode(
        $decoded,
        JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES
    );
} else {
    // اگر JSON معتبر نبود، همان خام را بفرست
    $pretty_body = $payload;
}

// escape کردن برای HTML
$pretty_body_safe = tgEscape($pretty_body);
$payload_raw_safe = tgEscape($payload);
$headers_json     = json_encode($headers, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
$headers_safe     = tgEscape($headers_json);

// پیام اول: متادیتا
$meta_text  = "📥 <b>دریافت جدید در وبهوک</b>\n\n";
$meta_text .= "🔹 <b>IP:</b> " . tgEscape($_SERVER['REMOTE_ADDR'] ?? '-') . "\n";
$meta_text .= "🔹 <b>Method:</b> " . tgEscape($_SERVER['REQUEST_METHOD'] ?? '-') . "\n";
$meta_text .= "🔹 <b>User-Agent:</b> " . tgEscape($_SERVER['HTTP_USER_AGENT'] ?? '-') . "\n";
$meta_text .= "🔹 <b>Content-Type:</b> " . tgEscape($_SERVER['CONTENT_TYPE'] ?? '-') . "\n";
$meta_text .= "🔹 <b>Body Length:</b> " . mb_strlen($payload) . " chars\n";

// پیام هدرها
$headers_text = "📋 <b>Headers (JSON):</b>\n<pre>" . $headers_safe . "</pre>";

// پیام بادی مرتبشده (Pretty JSON)
$body_pretty_text = "📦 <b>Body (Pretty JSON):</b>\n<pre>" . $pretty_body_safe . "</pre>";

// پیام بادی خام (Raw) - برای اطمینان
$body_raw_text = "🧾 <b>Body (Raw):</b>\n<pre>" . $payload_raw_safe . "</pre>";

// ارسال هر سه پیام (با تکهتکه کردن در صورت لزوم) - فقط اگر debug_chat_id تنظیم شده باشد
if (!empty($debug_chat_id)) {
    sendLongTelegramMessage($bot_token, $debug_chat_id, $meta_text,        'HTML');
    sendLongTelegramMessage($bot_token, $debug_chat_id, $headers_text,     'HTML');
    sendLongTelegramMessage($bot_token, $debug_chat_id, $body_pretty_text, 'HTML');
    sendLongTelegramMessage($bot_token, $debug_chat_id, $body_raw_text,    'HTML');
}

// ==== استخراج chat_id ====
$data    = $decoded;
$chat_id = null;

if (isset($data['message']['chat']['id'])) {
    $chat_id = $data['message']['chat']['id'];
} elseif (isset($data['callback_query']['message']['chat']['id'])) {
    $chat_id = $data['callback_query']['message']['chat']['id'];
} elseif (isset($data['edited_message']['chat']['id'])) {
    $chat_id = $data['edited_message']['chat']['id'];
}

$wait_msg_id = null;

// ==== ۱) بلافاصله پیام "صبر کنید" را بفرست ====
if ($chat_id) {
    $is_album = isset($data['message']['media_group_id']);
    $send_wait = true;
    $media_group_id = null;

    if ($is_album) {
        $media_group_id = $data['message']['media_group_id'];
        $lockFile = sys_get_temp_dir() . '/media_group_' . $media_group_id . '.lock';
        if (file_exists($lockFile)) {
            $send_wait = false;
        } else {
            file_put_contents($lockFile, time());
        }
    }

    if ($send_wait) {
        // گرفتن پاسخ تلگرام برای استخراج Message ID پیام "صبر کنید"
        $wait_response = sendTelegramMessage($bot_token, $chat_id, $wait_text);
        $wait_data = json_decode($wait_response, true);
        if (isset($wait_data['ok']) && $wait_data['ok'] && isset($wait_data['result']['message_id'])) {
            $wait_msg_id = $wait_data['result']['message_id'];
            if ($is_album) {
                file_put_contents($lockFile, $wait_msg_id);
            }
        }
    } else {
        $lockFile = sys_get_temp_dir() . '/media_group_' . $media_group_id . '.lock';
        if (file_exists($lockFile)) {
            $saved_id = file_get_contents($lockFile);
            if (is_numeric($saved_id)) {
                $wait_msg_id = (int)$saved_id;
            }
        }
    }

    http_response_code(200);
    header('Content-Type: application/json');
    echo json_encode(['ok' => true]);

    if (function_exists('fastcgi_finish_request')) {
        fastcgi_finish_request();
    } else {
        if (ob_get_level() > 0) {
            ob_end_flush();
        }
        flush();
    }
} else {
    http_response_code(200);
    header('Content-Type: application/json');
    echo json_encode(['ok' => true]);
    exit;
}

// ==== ۲) فوروارد payload به سرور ASP.NET Core ====
$ch = curl_init($dotnet_url);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
curl_setopt($ch, CURLOPT_POSTFIELDS, $payload);

$dotnet_headers = ['Content-Type: application/json'];
// اضافه کردن Message ID پیام وضعیت به هدرهای دات‌نت
if ($wait_msg_id) {
    $dotnet_headers[] = 'X-Wait-Message-Id: ' . $wait_msg_id;
}
// عبور توکن اسرارآمیز وبهوک (اگر تلگرام آن را فرستاده باشد) تا بک‌اند دات‌نت
// اصالت پیکر بار را تأیید کند
$tg_secret = $_SERVER['HTTP_X_TELEGRAM_BOT_API_SECRET_TOKEN']
    ?? ($headers['X-Telegram-Bot-Api-Secret-Token'] ?? null);
if (!empty($tg_secret)) {
    $dotnet_headers[] = 'X-Telegram-Bot-Api-Secret-Token: ' . $tg_secret;
}
curl_setopt($ch, CURLOPT_HTTPHEADER, $dotnet_headers);

curl_setopt($ch, CURLOPT_TIMEOUT, 10);
$response = curl_exec($ch);
$httpcode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
curl_close($ch);

// ==== ۳) بر اساس پاسخ سرور، پیام مناسب بفرست ====
if ($httpcode == 401 || $httpcode == 403) {
    sendTelegramMessage($bot_token, $chat_id, '⛔ دسترسی ندارید');
} elseif ($httpcode >= 200 && $httpcode < 300) {
    // موفق (دات نت خودش پیام را آپدیت می کند، نیازی به ارسال مجدد نیست)
} else {
    // اگر دات‌نت به هر دلیلی بالا نیامد یا تایم اوت شد، این پیام ارسال می‌شود
    sendTelegramMessage($bot_token, $chat_id, "⚠️ ارتباط با سرور قطع شد ($httpcode)\n$response");
}
?>
