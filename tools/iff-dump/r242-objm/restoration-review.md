# R242 original OBJM user-event phase import — independent review

## Scope and verified law

The approved restoration changes only surviving original OBJM stack frames in maintained `FreeSO/TSOClient/tso.simantics/Utils/VMTS1ActivatorNew.cs`. There is no UI geometry, save format, reader, original-data, JIT or general VM change. The root agent owns builds, runtime checks and packaging.

Fresh disassembly of the owned executable independently confirms the helper's core offset claim: `TreeStack::ReconStream` file1533a4 passes frame+8 to Recon32 at1533ac (destination11e7e0). The preceding fields are parameter/local Recon16 arrays; the following code-owner finder uses frame+c. `TryUserEvent` fac94 reads that same frame+8, facb8 writes1, and facd0 writes2. The maintained reader already stores this generic word in `OBJMStackFrame.PrimitiveState`. The existing importer had omitted the word. Executable SHA256 is pinned in `recover.py`, together with these exact instruction words.

The import now runs after the existing atomic `IsRestorableStack` rejection. For source states1/2, it resolves the existing marshaled code-owner GUID and routine using the established private/global/semiglobal lookup. Only current instruction opcode35 receives `TS1UserEventPhase`. State0, other generic values, non35 nodes, and rejected stacks retain the existing import behavior. This preserves the existing reader/native marshal40/JIT handler design.

## Rejected helper inference: House56 is not a saved PIP frame

The helper report correctly recovered a generic phase2 word but incorrectly joined it to Social2 by local tree ID4110 and node1 alone. Private routine IDs are only unique within their owning resource.

Independent original House56 parsing resolves object292's frame through actual OBJT entry146:

* GUID **bedd7b26**, OBJT label **Dunginator 9000**.
* Matching OBJD is **Pet - Cat - Litter Box** in `ExpansionPack5/ExpansionPack5.FAR!petcatlitterbox.iff`.
* Its private BHAV4110 is **Interaction - Cat - Use**; node1 is **opcode27**, not35.
* Social2's BHAV4110/node1 is opcode35, but its Social Attack object has GUID **9f6954fb**. None of Social2's OBJD GUIDs match House56 owner146.

Consequently this real saved state must remain unmapped. The source helper's claim of a literally verified saved PIP continuation is rejected. This review does not claim any original corpus frame is a confirmed saved opcode35 continuation; the serializer/primitive identity proves the import law independently, and positive fixtures use a real original opcode35 routine with synthetic saved phases.

`recover.py` re-reads original House56 OBJM/OBJT, resolves the actual GUID by OBJD across original FARs, reads its BHAV, compares Social2, and verifies the executable instruction pins. It passed and writes only derived text/JSON. `house56-identity.json` and `recovery-output.txt` record all seven original frames and the identity chain. The original House56 SHA256 is `8bb39d8e613bf6e3902e4a405bc26420d5d1f00b01e3f0301e471ec51497e800`.

## Focused runtime fixture

`AutotestOBJM242.Check(out string diagnostics)` invokes the production private `ConvertThread` via reflection in an isolated unticked VM:

* Actual Social Attack GUID9f6954fb/private4110/node1 proves the positive opcode35 fixture. Phases1 and2 import; states0,3,15,-1,256 remain0.
* Other stack fields, padded arguments, locals, temporary registers and source generic state remain unchanged.
* The production IFF/OBJM reader reads original House56; production OBJT mapping verifies owner146/GUIDbedd7b26 and opcode27. The actual frame, plus generic states1/2/3/15, stays unmapped despite matching Social2's tree/node numbers.
* Invalid routine, negative IP (unsigned stale index), and IP at routine length each accompany a valid positive frame and preserve the existing entire-stack rejection, empty queue and cleared interrupt contract.

The fixture only reads owned game files. Its import instances and arrays are isolated, and no lot is activated or ticked. Root hooked this helper as `uipip-original-import` and owns the packaged test result.

## Limits

The change deliberately does not reinterpret generic state values outside1/2, widen supported stale stacks, repair malformed OBJT indices, or decode other primitives' saved state. The helper's unrelated post-stack/compression uncertainties do not affect the newly verified frame+8/opcode35 identity. No visual validation is required for this import-only change; runtime continuation validation is provided by the isolated production-path helper and the existing PIP primitive tests.
