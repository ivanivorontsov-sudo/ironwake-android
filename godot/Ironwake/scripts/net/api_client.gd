extends Node
## HTTP meta client for Beget (cleartext HTTP allowed on Android export).

const DEFAULT_BASE := "http://biker9td.beget.tech"

var base_url: String = DEFAULT_BASE
var last_status: String = "idle"
var last_health_ok: bool = false

signal health_finished(ok: bool, body: String)
signal catalog_finished(ok: bool, vehicles: Array)
signal join_finished(ok: bool, body: String)
signal match_reported(ok: bool)


func configure(url: String) -> void:
	if not url.is_empty():
		base_url = url.rstrip("/")


func _request(method: String, path: String, body: String = "", timeout_sec: float = 8.0) -> Dictionary:
	var http := HTTPRequest.new()
	add_child(http)
	http.timeout = timeout_sec
	var headers := PackedStringArray(["Accept: application/json"])
	var err: Error
	if method == "POST":
		headers.append("Content-Type: application/json")
		err = http.request(base_url + path, headers, HTTPClient.METHOD_POST, body)
	else:
		err = http.request(base_url + path, headers, HTTPClient.METHOD_GET)
	if err != OK:
		http.queue_free()
		return {"ok": false, "code": -1, "body": "request error %s" % err}
	var result: Array = await http.request_completed
	http.queue_free()
	# result: [result, response_code, headers, body]
	var code: int = int(result[1])
	var text: String = (result[3] as PackedByteArray).get_string_from_utf8()
	var ok := int(result[0]) == HTTPRequest.RESULT_SUCCESS and code >= 200 and code < 300
	return {"ok": ok, "code": code, "body": text}


func health_check() -> void:
	last_status = "health…"
	var r := await _request("GET", "/health", "", 8.0)
	last_health_ok = bool(r.ok)
	last_status = "health ok" if r.ok else ("health fail: " + str(r.body))
	health_finished.emit(last_health_ok, str(r.body))


func fetch_catalog() -> void:
	last_status = "catalog…"
	var r := await _request("GET", "/catalog/vehicles", "", 10.0)
	if r.ok:
		var vehicles := VehicleCatalog.parse_catalog_json(str(r.body))
		GameState.catalog = vehicles
		last_status = "catalog ok (%d)" % vehicles.size()
		catalog_finished.emit(true, vehicles)
	else:
		last_status = "catalog fail — offline fallback"
		catalog_finished.emit(false, VehicleCatalog.fallback_list())


func join_room(callsign: String, vehicle_id: String, mode: String = "laststand") -> void:
	last_status = "join…"
	var payload := {
		"userId": GameState.user_id,
		"callsign": callsign,
		"vehicleId": vehicle_id,
		"mode": mode,
		"room": "public",
	}
	var r := await _request("POST", "/room/join", JSON.stringify(payload), 9.0)
	last_status = "join ok" if r.ok else ("join fail: " + str(r.body))
	join_finished.emit(bool(r.ok), str(r.body))


func report_match(result: Dictionary) -> void:
	var payload := {
		"userId": result.get("userId", GameState.user_id),
		"vehicleId": result.get("vehicleId", GameState.vehicle_id),
		"team": result.get("team", "blue"),
		"winner": result.get("winner", ""),
		"victory": result.get("victory", false),
		"survived": result.get("survived", false),
		"duration": result.get("duration", 0.0),
		"kills": result.get("kills", 0),
		"mode": result.get("mode", "local_laststand"),
	}
	var r := await _request("POST", "/match", JSON.stringify(payload), 10.0)
	last_status = "POST /match ok" if r.ok else "POST /match fail"
	match_reported.emit(bool(r.ok))
