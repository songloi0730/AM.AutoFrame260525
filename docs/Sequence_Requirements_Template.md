# Sequence_Requirements — [Tên máy tham khảo]

> **Cách dùng:** Mở phiên Claude Code riêng, cho đọc dự án tham khảo, điền file này.
> Đầu ra là **hành vi và yêu cầu**, không phải code. KHÔNG chép class/method/cấu trúc file.
> Sau khi điền xong → đóng dự án tham khảo → thiết kế engine chỉ từ file này.
> File này cũng là căn cứ lọc feedback reviewer: đề xuất nào không truy được về
> một dòng trong file này thì xếp loại "ý kiến", không phải "yêu cầu".

## 0. Thông tin chung

| Mục | Giá trị |
|---|---|
| Tên máy / loại máy | |
| Số trạm (station) | |
| Kiến trúc điều khiển | (PC-based / PLC-based / hybrid) |
| Nhịp máy (cycle time mục tiêu) | |
| Sản phẩm đi qua máy theo | (carrier / tray / rời từng cái) |

## 1. Danh sách trạm và vai trò

> Mỗi trạm một dòng. "Loại bước" chọn: Motion / Đo-kiểm / Vision / Giao tiếp / Ghi dữ liệu / Chờ điều kiện.

| # | Trạm | Vai trò một câu | Loại bước | Phụ thuộc trạm nào | Tài nguyên dùng chung |
|---|---|---|---|---|---|
| 1 | | | | | (trục? camera? cổng COM?) |
| 2 | | | | | |

## 2. Vòng đời máy

- [ ] Thứ tự khởi tạo (kết nối thiết bị → homing trục nào trước → về vị trí chờ):
- [ ] Điều kiện để được phép Start (cửa đóng? áp khí OK? recipe đã nạp? homing xong?):
- [ ] Sau khi Start, máy chạy theo (một sản phẩm/lượt · liên tục cho tới hết liệu · N sản phẩm):
- [ ] Điều kiện tự dừng (hết liệu, khay ra đầy, đạt số lượng):

## 3. Ngữ nghĩa lệnh vận hành

> Phần quan trọng nhất. Mỗi lệnh trả lời: dừng Ở ĐÂU, trục/van ở trạng thái gì, resume từ đâu.

| Lệnh | Máy tham khảo xử lý thế nào | Ghi chú |
|---|---|---|
| Pause | (dừng ngay giữa bước / dừng ở ranh giới bước?) | |
| Resume | (chạy tiếp bước dở / làm lại bước từ đầu?) | |
| Stop | (hoàn thành sản phẩm đang dở rồi dừng / hủy ngay?) | |
| Abort / E-Stop | (điều gì bị cắt cứng, điều gì cần homing lại?) | |
| Reset | (từ trạng thái nào được phép? xóa những gì?) | |

## 4. Chính sách lỗi theo trạm

| Trạm | Timeout | Khi lỗi | Số lần retry | Hết retry thì | Lỗi này có làm sản phẩm NG không |
|---|---|---|---|---|---|
| | | (Retry/Skip/Pause/Abort) | | (Pause gọi operator / Abort) | |

- [ ] Phân biệt của máy tham khảo giữa **lỗi máy** (thiết bị hỏng) và **NG** (sản phẩm xấu):
- [ ] Sản phẩm NG được xử lý thế nào (đặt khay NG / đánh dấu / dừng máy?):
- [ ] Trạm nào được phép Skip mà sản phẩm vẫn tính OK:

## 5. Song song hóa

- [ ] Các trạm chạy đồng thời:
- [ ] Ràng buộc chống tranh chấp (hai trạm cùng cần trục Z? cùng camera?):
- [ ] Có pipeline không (sản phẩm N ở trạm 3 trong khi N+1 ở trạm 1)?:

## 6. Dữ liệu sản phẩm và traceability

- [ ] SN đến từ đâu (scan / host cấp / máy tự sinh):
- [ ] Mỗi trạm ghi lại dữ liệu gì (đối chiếu cột dữ liệu trên UI máy tham khảo):
- [ ] Phán định OK/NG cuối cùng dựa trên:
- [ ] Dữ liệu upload đi đâu, lúc nào (theo từng sản phẩm / theo lô / cuối ca):
- [ ] Khi upload lỗi thì máy có dừng không:

## 7. Chế độ vận hành

| Chế độ | Có/Không | Hành vi khác Auto ở chỗ nào |
|---|---|---|
| Dry run (chạy không liệu) | | (bỏ trạm nào? vacuum tắt?) |
| Single-step | | (dừng sau mỗi bước chờ xác nhận?) |
| Manual / jog | | (yêu cầu quyền gì, khóa liên động gì) |

## 8. An toàn tương tác với sequence

- [ ] Mở cửa khi đang chạy → máy làm gì:
- [ ] E-Stop nhả ra → cần những bước nào trước khi chạy lại:
- [ ] Trạng thái van/vacuum khi mất khí, mất điện:

## 9. Log mỗi bước

- [ ] Máy tham khảo ghi log gì mỗi bước (đối chiếu bảng 运行日志 / run log):
- [ ] Mức log (bước bắt đầu/kết thúc, giá trị đo, thời gian từng bước):

## 10. Anti-pattern ghi nhận — KHÔNG bắt chước

> Liệt kê những gì thấy trong code tham khảo mà AM.AutoFrame sẽ làm khác đi, kèm lý do.

| Anti-pattern thấy được | AM.AutoFrame làm thay bằng |
|---|---|
| (vd: switch-case cứng theo tên trạm) | Sequence là dữ liệu, engine generic |
| (vd: station gọi thẳng vendor DLL) | HAL — logic không tham chiếu vendor type |
| | |
