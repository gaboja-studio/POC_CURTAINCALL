# 직렬화 필드 파일별 목록 6

소스의 명시적 SerializeField/SerializeReference 선언이다. 참조·외형·배치·게임 수치를 함께 포함하며, 전부 통일할 대상이라는 뜻은 아니다. 실제 값은 씬·프리팹에 저장될 수 있다.

| 소스 | 필드 |
|---|---|
| [StructureAExitRoute.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/Exploration/StructureAExitRoute.cs:15) | `StructureAExitRoute.restroomBreach`, `StructureAExitRoute.key2`, `StructureAExitRoute.machineDoor`, `StructureAExitRoute.passwordPad`, `StructureAExitRoute.escapeDoor`, `StructureAExitRoute.escapePassword` |
| [StructureAKeyRoute.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/Exploration/StructureAKeyRoute.cs:24) | `StructureAKeyRoute.key1`, `StructureAKeyRoute.steelBox`, `StructureAKeyRoute.managementKey`, `StructureAKeyRoute.lockedBox1`, `StructureAKeyRoute.stairsKey`, `StructureAKeyRoute.stairsDoor`, `StructureAKeyRoute.cabinet3`, `StructureAKeyRoute.managementDrawer`, `StructureAKeyRoute.escapeKey1`, `StructureAKeyRoute.escapeKey2`, `StructureAKeyRoute.escapeDoor` |
| [ToolTarget.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/Exploration/ToolTarget.cs:17) | `ToolTarget.targetId`, `ToolTarget.kind`, `ToolTarget.label`, `ToolTarget.reach`, `ToolTarget.blocker`, `ToolTarget.hinge`, `ToolTarget.openEuler`, `ToolTarget.hideOnOpen`, `ToolTarget.showOnOpen`, `ToolTarget.rewardItemId`, `ToolTarget.rewardPoint`, `ToolTarget.rewardUidOffset`, `ToolTarget.sound` |
| [UvFlashlight.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/Exploration/UvFlashlight.cs:18) | `UvFlashlight.color`, `UvFlashlight.range`, `UvFlashlight.spotAngle`, `UvFlashlight.intensity`, `UvFlashlight.frontLimit`, `UvFlashlight.ringReach`, `UvFlashlight.requestInterval`, `UvFlashlight.checkInterval` |
| [UvMark.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/Exploration/UvMark.cs:15) | `UvMark.targetId`, `UvMark.label`, `UvMark.reach`, `UvMark.marks`, `UvMark.sound`, `UvMark.glow`, `UvMark.holdSeconds` |
| [ExplorationTimerAnnouncer.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/ExplorationTimer/ExplorationTimerAnnouncer.cs:33) | `ExplorationTimerAnnouncer.broadcastSeconds` |
| [ExplorationTimerView.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/ExplorationTimer/ExplorationTimerView.cs:20) | `ExplorationTimerView.announcer`, `ExplorationTimerView.showLegacyUi`, `ExplorationTimerView.font`, `ExplorationTimerView.bellClip`, `ExplorationTimerView.bellVolume`, `ExplorationTimerView.messageDuration`, `ExplorationTimerView.normalColor`, `ExplorationTimerView.alarmColor`, `ExplorationTimerView.pausedColor` |
| [ExplorationTimerZones.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/ExplorationTimer/ExplorationTimerZones.cs:18) | `ExplorationTimerZones.runningZones`, `ExplorationTimerZones.areas` |
| [ItemCatalog.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/Items/ItemCatalog.cs:52) | `ItemCatalog.items` |
| [NightBed.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/NightSchedule/NightBed.cs:17) | `NightBed.body`, `NightBed.reach` |
| [NightDoorLock.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/NightSchedule/NightDoorLock.cs:16) | `NightDoorLock.shutter`, `NightDoorLock.lockedDoor`, `NightDoorLock.warningLamp`, `NightDoorLock.moveSeconds`, `NightDoorLock.openScale`, `NightDoorLock.blinkSeconds`, `NightDoorLock.notice`, `NightDoorLock.showLegacyNotice` |
| [NightPower.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/NightSchedule/NightPower.cs:9) | `NightPower.circuit` |
| [NightScheduleTestUI.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/NightSchedule/NightScheduleTestUI.cs:22) | `NightScheduleTestUI.cheers`, `NightScheduleTestUI.stepTimeout` |
| [ScheduleBoard.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/NightSchedule/ScheduleBoard.cs:19) | `ScheduleBoard.selectableTricks`, `ScheduleBoard.trickNames` |
| [ScheduleBoardView.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/NightSchedule/ScheduleBoardView.cs:18) | `ScheduleBoardView.board`, `ScheduleBoardView.closeUp`, `ScheduleBoardView.reach`, `ScheduleBoardView.showLegacyUi`, `ScheduleBoardView.closeUpPriority` |
| [DebugMoneyProp.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/ShopStorage/DebugMoneyProp.cs:12) | `DebugMoneyProp.amount`, `DebugMoneyProp.reach` |
| [FloorPropsView.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/ShopStorage/FloorPropsView.cs:14) | `FloorPropsView.reach`, `FloorPropsView.itemSize`, `FloorPropsView.color`, `FloorPropsView.showLegacyLabels` |
| [HandoverShelfView.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/ShopStorage/HandoverShelfView.cs:14) | `HandoverShelfView.reach`, `HandoverShelfView.itemSize`, `HandoverShelfView.color`, `HandoverShelfView.boards`, `HandoverShelfView.showLegacyLabels`, `HandoverShelfView.visualRoot` |
| [HeldPropsView.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/ShopStorage/HeldPropsView.cs:15) | `HeldPropsView.itemSize`, `HeldPropsView.firstOffset`, `HeldPropsView.stack`, `HeldPropsView.itemColor`, `HeldPropsView.showLegacyLabels` |
| [PropCarry.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/ShopStorage/PropCarry.cs:27) | `PropCarry.floorCapacity`, `PropCarry.dropAhead`, `PropCarry.showHud` |
| [SharedFundsView.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/ShopStorage/SharedFundsView.cs:13) | `SharedFundsView.show` |
| [ShopDeliverySlot.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/ShopStorage/ShopDeliverySlot.cs:26) | `ShopDeliverySlot.reach`, `ShopDeliverySlot.dropForward`, `ShopDeliverySlot.perRow`, `ShopDeliverySlot.spacing`, `ShopDeliverySlot.itemSize`, `ShopDeliverySlot.dropTime`, `ShopDeliverySlot.showLegacyLabels` |
| [ShopPhone.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/ShopStorage/ShopPhone.cs:16) | `ShopPhone.reach`, `ShopPhone.showLegacyUi` |
| [ShopStorageTestUI.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/ShopStorage/ShopStorageTestUI.cs:22) | `ShopStorageTestUI.cheers`, `ShopStorageTestUI.stepTimeout` |
| [WaitingRoomShop.cs](C:/Unity/POC_CURTAINCALL/Assets/Scripts/WaitingRoom/ShopStorage/WaitingRoomShop.cs:33) | `WaitingRoomShop.items`, `WaitingRoomShop.slotCapacity` |
