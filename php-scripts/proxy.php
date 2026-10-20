<?php
// proxy.php
// Proxies outbound requests from ASP.NET Core to api.telegram.org

// MUST match TelegramProxy:Secret in the .NET app (user-secrets / env).
$config = file_exists(__DIR__ . '/config.php') ? require __DIR__ . '/config.php' : [];
$secret = $config['proxy_secret'] ?? "SecureSecret12345";

// Security check
if (!isset($_SERVER['HTTP_X_PROXY_SECRET']) || $_SERVER['HTTP_X_PROXY_SECRET'] !== $secret) {
    http_response_code(403);
    die("Unauthorized");
}

$target_url = "https://api.telegram.org";

// Extract the path from the 'req' query parameter
$path = isset($_GET['req']) ? $_GET['req'] : '';

// Remove 'req' from query string to forward any other GET parameters (if any)
$queryString = '';
if (isset($_SERVER['QUERY_STRING'])) {
    parse_str($_SERVER['QUERY_STRING'], $queryParams);
    unset($queryParams['req']);
    if (!empty($queryParams)) {
        $queryString = '?' . http_build_query($queryParams);
    }
}

$url = $target_url . $path . $queryString;

$ch = curl_init($url);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
curl_setopt($ch, CURLOPT_CUSTOMREQUEST, $_SERVER['REQUEST_METHOD']);

$headers = [];
// Forward necessary headers (especially Content-Type for multipart/form-data)
foreach (getallheaders() as $name => $value) {
    $lowerName = strtolower($name);
    if ($lowerName !== 'host' && $lowerName !== 'x-proxy-secret' && $lowerName !== 'content-length' && $lowerName !== 'transfer-encoding') {
        // Skip the customized Content-Type from .NET, we will handle it below
        if ($lowerName === 'content-type') {
            continue;
        }
        $headers[] = "$name: $value";
    }
}

if ($_SERVER['REQUEST_METHOD'] !== 'GET') {
    $contentType = isset($_SERVER["CONTENT_TYPE"]) ? $_SERVER["CONTENT_TYPE"] : '';
    
    // If .NET bypassed PHP parsing by sending 'application/x-proxy-multipart'
    if (strpos($contentType, 'application/x-proxy-multipart') !== false) {
        // Revert it back to standard multipart/form-data
        $contentType = str_replace('application/x-proxy-multipart', 'multipart/form-data', $contentType);
    }
    
    if (!empty($contentType)) {
        $headers[] = "Content-Type: $contentType";
    }

    // Now php://input contains the pure raw body (bypassed PHP's parsing!)
    curl_setopt($ch, CURLOPT_POSTFIELDS, file_get_contents('php://input'));
}

curl_setopt($ch, CURLOPT_HTTPHEADER, $headers);

$response = curl_exec($ch);
$httpcode = curl_getinfo($ch, CURLINFO_HTTP_CODE);

// Get response content type
$contentType = curl_getinfo($ch, CURLINFO_CONTENT_TYPE);
curl_close($ch);

http_response_code($httpcode);
if ($contentType) {
    header("Content-Type: " . $contentType);
}
echo $response;
?>
