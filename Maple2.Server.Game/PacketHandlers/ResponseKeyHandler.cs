﻿using System;
using Grpc.Core;
using Maple2.Database.Storage;
using Maple2.PacketLib.Tools;
using Maple2.Server.Core.Constants;
using Maple2.Server.Core.PacketHandlers;
using Maple2.Server.Core.Packets;
using Maple2.Server.Game.Session;
using Maple2.Server.World.Service;
using Microsoft.Extensions.Logging;
using static Maple2.Model.Error.MigrationError;
using WorldClient = Maple2.Server.World.Service.World.WorldClient;

namespace Maple2.Server.Game.PacketHandlers;

public class ResponseKeyHandler : PacketHandler<GameSession> {
    public override ushort OpCode => RecvOp.RESPONSE_KEY;

    #region Autofac Autowired
    // ReSharper disable MemberCanBePrivate.Global
    public WorldClient World { private get; init; } = null!;
    // ReSharper restore All
    #endregion

    public ResponseKeyHandler(ILogger<ResponseKeyHandler> logger) : base(logger) { }

    public override void Handle(GameSession session, IByteReader packet) {
        long accountId = packet.ReadLong();
        ulong token = packet.Read<ulong>();
        var machineId = packet.Read<Guid>();

        try {
            logger.LogInformation("LOGIN USER TO GAME: {AccountId}", accountId);

            var request = new MigrateInRequest {
                AccountId = accountId,
                Token = token,
                MachineId = machineId.ToString(),
            };
            MigrateInResponse response = World.MigrateIn(request);
            if (!session.EnterServer(accountId, response.CharacterId, machineId)) {
                throw new InvalidOperationException($"Invalid player: {accountId}, {response.CharacterId}");
            }

            // Finalize
            session.Send(Packet.Of(SendOp.WORLD));
        } catch (Exception ex) when (ex is RpcException or InvalidOperationException) {
            session.Send(MigrationPacket.MoveResult(s_move_err_default));
            session.Disconnect();
        }
    }
}
