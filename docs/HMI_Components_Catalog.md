# HMI Components Catalog — Thành phần từng màn hình (IPC 1920×1080, 21–24")

> **Mục đích:** Danh mục thành phần/tham số nên có cho TỪNG màn hình của máy tự động hoá trên IPC lớn,
> dùng làm checklist khi xây dựng từng screen. Bổ trợ cho:
> - `docs/HMI_UI_Architecture_Template.md` — khung layout + ISA-101/SEMI E95
> - `.claude/skills/am-hmi-design/SKILL.md` — quy tắc thiết kế (đọc TRƯỚC khi làm UI)
>
> **Target:** IPC 1920×1080, 21–24", chuột + cảm ứng. Triết lý ISA-101 High-Performance HMI (yên tĩnh khi bình thường).

---

## Nguyên tắc chung
- Trạng thái máy nhìn thấy trong **1–2 giây**; lỗi có mô tả + hướng xử lý + mức độ.
- Lệnh nguy hiểm phải **xác nhận**; nút khoá khi điều kiện chưa đạt + hiện lý do khoá; chống double-click.
- Mỗi màn **1 mục tiêu chính**; thông tin kỹ thuật sâu để ở màn con/popup.
- Màu trạng thái nhất quán: **xanh lá** ready/run · **vàng** warning/interlock/manual · **đỏ** alarm/e-stop · **xanh dương** busy · **xám** offline/disable.

---

## 1. Dashboard (L1)
- **Tổng quan:** tên máy/line/trạm · PackML state (Idle/Running/Pause/Alarm/E-Stop/Manual) · user+quyền · giờ/ca/ngày · version SW/recipe/mã sản phẩm.
- **Sản xuất:** OK/NG · total · takt time · cycle time gần nhất · target/ca · pass-fail % · tình trạng (Running/Waiting material/Waiting operator/Fault/Up-Downstream stop).
- **Trạng thái máy:** interlock tổng · cửa an toàn/light curtain/E-stop · vacuum/air pressure · servo/heater/conveyor/robot/cylinder · alarm hiện tại + gần nhất.
- **Kết nối thiết bị:** chip icon+màu+text cho PLC/RFID/Camera/MES/HIVE/SECS-GEM/Barcode/Printer/Scale/Robot/DB (Connected/Connecting/Error/Timeout + last data time, click xem chi tiết).
- **Nút nhanh:** Start/Stop/Pause-Resume/Reset alarm/Home all/Load recipe/Manual/Auto.
- **Bố cục gợi ý:** trái = máy+user · giữa = run+số lượng · phải = kết nối+alarm · dưới = nút nhanh + footer.

## 2. Auto / Run screen (L2)
- Trạng thái từng bước chu trình: step hiện tại/trước/tiếp · timer từng bước · interlock từng công đoạn · sensors liên quan.
- **Flow chart** đơn giản: công đoạn xong (xám/xanh nhạt), đang chạy (nổi bật), lỗi/chờ (trạng thái riêng).
- Chia **module theo vùng** (Nạp liệu/Định vị/Gia công/Kiểm tra/Gắp-đặt/Xả/Phân loại/Đóng gói): mỗi module có enable/disable · ready/busy/fault · sensor chính · manual override (nếu phép).
- Số chu kỳ từ reset · tự dừng sau số lượng đặt · nhắc bảo trì theo chu kỳ · số sản phẩm giữ lại do lỗi.

## 3. IO Monitor (L3)
- Nhóm: DI · DO · AI · AO · Safety I/O · Motion I/O · Communication/PLC bits.
- Mỗi điểm: tên signal · địa chỉ PLC/module · trạng thái · mô tả chức năng · nhóm công đoạn · điều kiện bật/tắt.
- Chức năng: tìm theo tên · lọc theo nhóm/trạng thái · real-time · **test/force output chỉ ở service mode** (hiện "forced", ghi log).
- Hiển thị: DI/DO xanh ON/xám OFF, cam nếu force · Safety riêng nổi bật · Analog: giá trị+đơn vị+min/max+cảnh báo ngưỡng.

## 4. Settings (tab, đừng gom 1 màn)
General · Machine · Recipe · Alarm · Communication · Motion · Vision · I/O mapping · Access control · Maintenance.
- **General:** ngôn ngữ · đơn vị (mm/inch/ms/deg) · ngày giờ/timezone · độ sáng · âm thanh cảnh báo · auto-logout · advanced/basic.
- **Communication:** IP/subnet/gateway · protocol (TCP/Modbus/OPC-UA/EtherNet-IP/Profinet/SECS-GEM) · baud/parity · timeout · retry · heartbeat · port · enable từng kênh.
- **I/O mapping:** gán tên logic ↔ địa chỉ vật lý · mapping theo version · import/export · so sánh backup.
- **Access control:** tài khoản · vai trò (operator/technician/engineer/admin) · quyền từng trang/sửa tham số/test IO/reset thống kê.

## 5. Motion / Axis Settings (L3) + Motion Overview
- **Cơ bản từng trục:** tên (X/Y/Z/R/Theta/Conveyor/Clamp) · loại motor · driver model · node ID · enable · ready/alarm/home/moving.
- **Tham số:** unit scale (pulse/mm) · direction invert · home dir/offset · soft limit ±/hard limit · jog speed low/high · max speed · accel/decel · S-curve/jerk · in-position window · stop mode.
- **Homing:** sequence type · search speed · backoff · home sensor type · limit handling · timeout · re-home after alarm.
- **Position:** preset list · teach · relative/absolute · return safe · stored per recipe.
- **Servo:** enable delay · torque limit · gains · following error limit · alarm clear · brake · encoder.
- **Jog/test:** jog ±/single-step/home/move-to-taught/soft-limit check.
- **Cảnh báo riêng:** servo alarm/overload/over-speed/following error/encoder/homing failed/soft limit.
- **Motion Overview (bảng):** Axis | Position | Speed | Servo | Home | Alarm + current/target/actual/following error/torque/load.

## 6. Calibration (L4 — wizard từng bước)
- Loại: camera · camera-to-robot (hand-eye) · nozzle/head offset · pick/place position · fiducial · tray/feeder · Z-height/touch · theta.
- Thành phần màn: wizard Next/Back/progress · live camera + overlay (lưới/tâm/ROI) · jog rút gọn · Teach/Capture · bảng kết quả (đo/sai số/so cũ/pass-fail) · Save/Apply/Restore · lịch sử calib (time/user/kết quả).
- An toàn: cho huỷ giữa chừng + khôi phục · không ghi đè tới khi Save · cảnh báo nếu lệch quá nhiều · cảnh báo calib quá hạn.

## 7. Alarm / Event log (trung tâm chẩn đoán)
- **Alarm list:** mã · mô tả · thời gian · station/module · mức · trạng thái (active/acked/cleared) · số lần lặp.
- **Event log:** start/stop · change recipe · login/logout · change parameter · manual override · calibration save · comm loss/restore.
- Sắp theo thời gian · lọc theo loại/mức · tìm theo mã · export CSV/PDF · lời khuyên xử lý mỗi mã.

## 8. Manual / Jog (chỉ Manual/Service + xác nhận quyền cao)
- Điều khiển từng cơ cấu (vacuum/cylinder/motor/pump/heater/fan) · jog từng trục · chạy từng bước · test từng output · test camera trigger/RFID/barcode/conveyor.
- Hiện rõ cơ cấu nào đang điều khiển · nút reset vị trí an toàn sau test.

## 9. Recipe / Program
- Dữ liệu: tên · mã SP · version · kích thước/thông số · threshold camera · position setpoint · timing · IO option theo SP.
- Chức năng: New/Copy/Edit/Delete/Import/Export/Lock-Unlock/Compare/Default.
- Version: number · người sửa · thời gian · ghi chú · khôi phục version cũ. Validate trong khoảng trước khi chạy.

## 10. Connectivity / Device Monitor (L4)
- Mỗi thiết bị: tên · loại giao tiếp · IP/port/node · online/offline · handshake · last heartbeat · last error · latency · data exchange.
- **RFID:** reader ready · last EPC · last read time · read/error count · read/write ok/fail.
- **Camera:** connected · last trigger/capture · FPS · inspection pass/fail.
- **Barcode:** last scan · pass rate · scanner status. **Printer:** ready · last job · remaining label · ribbon.
- **MES:** login · job download · result upload · heartbeat · request/response/timeout count · log TX/RX/ACK/NAK.
- **SECS/GEM:** COMM state · CONTROL state · equipment state (INIT/IDLE/SETUP/READY/EXECUTE/STOP/ABORT) · event report · host command.

## 11. User / Permission
- Tài khoản: username · full name · role · last login · status. Quản trị: add/edit/delete · reset password · lock · auto-logout.
- Operator (chạy/xem) · Technician (manual/test/reset nhẹ) · Engineer (tham số/calib) · Admin (user/system/backup/comm).

## 12. Maintenance / Service
- Số chu kỳ/giờ vận hành · lịch bảo trì · phụ tùng cần thay · sensor wear · motor/pump/filter time.
- Reset counter sau thay thế · test toàn bộ actuator · dry run · checklist · export service log.

## 13. History / Production report
- Số lượng theo ca/ngày/tháng · OK/NG % · downtime theo nguyên nhân · alarm frequency · dừng lâu nhất · utilization · export Excel/CSV.

## 14. OEE Dashboard (nâng cao)
- Availability × Performance × Quality = OEE · top-10 alarm · downtime analysis · production trend · hourly output.

## 15. Vision Setup (tách khỏi Calibration)
- Camera params: exposure/gain/gamma/brightness/contrast/white balance.
- Inspection: threshold/blob area/match score/rotation range/ROI. Live view ≥ 50% màn (cross-hair, result/NG overlay). Pass/fail count + rate.

## 16. Traceability
- SP: SN/lot/time/operator/recipe. Quá trình: RFID/barcode/vision result/torque/force/position. Truy xuất theo SN/lot/date.

## 17. System / Backup / Update
- Version SW/PLC · backup recipe/calibration/log · restore · update package · device info · disk usage · network.

---

## Thành phần dùng chung mọi trang
- **Status bar trên:** machine state · alarm summary · connection summary · user · time · recipe.
- **Nav nhanh:** Home/Auto/Manual/IO/Alarm/Settings/Maintenance.
- **Bảo vệ thao tác:** xác nhận lệnh nguy hiểm · disable nút khi chưa đủ điều kiện + hiện lý do · chống double-click.
