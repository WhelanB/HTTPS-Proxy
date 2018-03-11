# HTTPS-Proxy
A HTTP/S Proxy with WebSocket support and a web-management console

The Proxy handles both HTTP and HTTPS connections, and also provides WebSocket support.
Once started, the Proxy server maps to port 80, and the management console to port 2000.
The management console provides a web-view of the console output of the application via
Websockets, and also provides a number of commands and a filter-list.

## Commands
|Command Name|Parameters|Description|
|---|---|---|
|add-filter|string|add a host to the filter|
|remove-filter|string|remove a host from the filter|
|list-filter|none|list the filter list in the console|
|set-log|string "None" "Debug" or "Verbose"|set the logging level for the web console|
|stop|none|terminate the proxy|

