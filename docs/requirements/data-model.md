# 学生数据格式

单学生一文件，如 `data/张三.json`，一个文件同时包括学期设置、课时和课程。使用UTF-8 JSON。以下字段名和语义已经确认，不任意改名。姓名取自文件名，不重复保存在JSON内。实现划分和第一版采用的数值范围见[设计方案](../design/program-design.md)。

下例演示双节学生；新建学生默认classesUnit为one，已有文件中的明确模式照常加载。

```json
{
  "schemaVersion": 1,
  "settings": {
    "firstWeekMonday": "2026-08-31",
    "totalWeeks": 20,
    "sundayBelongsTo": "next",
    "classesPerDay": 8,
    "classesUnit": "two",
    "timeGeneration": {
      "durationMinutes": 45,
      "oddBreakMinutes": 10,
      "evenBreakMinutes": 20,
      "morningStart": "08:00",
      "afternoonStart": "14:00",
      "eveningStart": "19:00"
    },
    "classPeriods": [
      { "period": 1, "start": "08:00", "end": "08:45" },
      { "period": 2, "start": "08:55", "end": "09:40" },
      { "period": 3, "start": "10:00", "end": "10:45" },
      { "period": 4, "start": "10:55", "end": "11:40" },
      { "period": 5, "start": "14:00", "end": "14:45" },
      { "period": 6, "start": "14:55", "end": "15:40" },
      { "period": 7, "start": "16:00", "end": "16:45" },
      { "period": 8, "start": "16:55", "end": "17:40" }
    ]
  },
  "courses": [
    {
      "id": "712aeb61-206b-4920-bdba-397d54f6df74",
      "name": "高等数学",
      "color": "#A8CEFF",
      "note": "",
      "schedules": [
        { "id": "0c337bb5-e19f-496f-b450-a9844cc41e66", "weeks": [1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16], "dayOfWeek": 1, "startPeriod": 1, "location": "A101" },
        { "id": "ffcb76f4-6c67-4da4-9287-d94ad7caf911", "weeks": [3,5,7,9], "dayOfWeek": 3, "startPeriod": 3, "location": "" }
      ]
    }
  ]
}
```

约定：`dayOfWeek` 周一=1 … 周日=7；`startPeriod` 在双节模式表示一组配对的第一节，结束节由课程模式推导，不在每条时段混入不同模式；`weeks` 为该课程时段实际出现的周次集合；使用 ISO 日期与 24 小时 `HH:mm`，显示层可采用 `yyyy/MM/dd`。颜色由 course ID 关联，同名但不同课程允许独立颜色。保存时先写临时文件再原子替换，降低损坏风险；文本编码 UTF-8。路径必须相对可执行文件目录（而非当前命令行工作目录）。

## 字段及校验约束

- schemaVersion=1。第一周周一必须为星期一，totalWeeks为正整数；sundayBelongsTo只允许previous/next。
- classesPerDay只允许8/10；classesUnit只允许one/two，默认one，不在课程或时段中重复保存。已保存课程数0时允许更改，有课程时禁止；先删除全部课程并保存后方可再改。切换学生加载其JSON里的设置及课程，不套用别人的配置。
- classPeriods完整覆盖1..classesPerDay，各节开始早于结束，按节次时间有序且不重叠。时长和实际课后间隔只计算、不存储。
- timeGeneration只保存生成参数；修改参数不会自动覆盖classPeriods。上午、下午、晚上各自从起点生成，奇偶课间均为0–99分钟整数，默认10/20；生成结果不允许时间重叠或跨午夜。生成起点的UI选项限制不用于静默修改已有文件。
- 课程和时段使用唯一GUID。name非空，color严格为7个字符的#RRGGBB（不接受尾随换行或空白），note可为空字符串。课程至少包含一条有效时段。
- weeks为非空、升序、无重复的整数集合，范围1..totalWeeks；dayOfWeek范围1..7。
- one模式startPeriod范围1..classesPerDay；two模式只允许配对起点1/3/5/7，10节时另有9。
- location允许为空，统一保存为""。不保存当前浏览周、自动计算的当前周、通知或重复的实际日期。
- 同一学生同一周、同一天占用相同节次时禁止保存，含同门课程内部的重复安排。
- 读取或保存失败保留原文件，底部信息栏提示，不用空数据覆盖。临时文件与目标文件同目录，校验成功才替换。

**关键**：上一周/下一周指周日归属于哪一周，并非将周日课程从一个星期改到另一个日期。每行课程按 `weekIndex + dayOfWeek` 映射到真实日期。不允许跨学生共享设置。

切换归属时，使用旧设置先计算实际日期，再依据新设置重算周次；自动按需前移学期起点或扩展总周数。所有课程日期保持不变，设置和课程作为一次完整保存提交。

## 周次与时间显示（不存入JSON）

WeekSelectionParser仅接受ASCII数字、英文连字符/逗号/空格，Parse生成去重升序周次，Format压缩连续区间，如1-5, 7, 8规范化为1-5, 7-8。

CourseTimeCode.EncodeTime/DecodeTime处理不带周次的时间码：一位星期（周日7）+两位开始节，双节再追加两位结束节，如401、30102。验证ASCII数字、长度、星期、节次、模式及配对。原Encode/Decode/Format保留原有接口兼容，但当前窗口不使用带周次编码。

管理表按课程ID分组，每条结构化时段一行；SchedulePresentation统一排序和显示。schemaVersion仍为1，不新增冗余编码或周次字符串字段。颜色仍保存#RRGGBB，新建建议色不改变已有颜色。
