// DotnetOpenEhr AOT/trim smoke test.
//
// This executable is published with PublishAot=true in CI. Its only
// job is to exercise the publishable SDK surface end-to-end so that
// any new trim/AOT warning fails the build. Phases 1..9 each extend
// this file as their packages come online; Phase 0 ships only the
// console banner so the gate is alive from day one.

using DotnetOpenEhr.Foundation.Iso;

IsoDate date = IsoDate.Parse("2024-05-27");
IsoDateTime dt = IsoDateTime.Parse("2024-05-27T10:25:03Z");
IsoDuration dur = IsoDuration.Parse("P1Y2M3DT4H5M6.789S");

System.Console.WriteLine($"foundation: {date} / {dt} / {dur}");
System.Console.WriteLine("smoke ok");
return 0;

