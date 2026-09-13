# Controller knowledge base

What an NCXchange engineer needs to know about the controller families before writing a reader or a compiler, collected in one place so that nothing outside this repository has to be consulted for the first implementation. The specification tells what NCX means; these documents tell what the controllers do, in their own terms, and where their habits differ.

| Document | Content |
|---|---|
| `differences.md` | The same twelve things (feed, arcs, tool call, offsets, datum, cycles, program frame, comments, machine datum, numbers, incremental words, M functions) side by side for the three families; read this first |
| `fanuc.md` | Fanuc Series 0i/16i/18i/21i/30i/31i and the ISO dialects built on them (Mazak EIA, Hyundai WIA, Matsuura, Mori Seiki MAPPS, Doosan, Nakamura, Biglia): program file, numbers, modality and G groups, motion, tools, cycles, custom macro B, multi-path, reader and writer rules |
| `heidenhain.md` | Heidenhain iTNC 530 and TNC 640 Klartext: program file, numbers, motion, tool call, cycles with Q parameters, PLANE, TCPM, Q parameters and FN functions, labels, turning mode of the TNC 640 |
| `siemens.md` | SINUMERIK 840D and 840D sl: program units, addresses with `=`, G groups, frames, motion, feed, tools, spindles, cycles with their signatures, flow and subprograms, channels, system variables, ISO mode |
| `machine-builders.md` | What the machine builders add on top of the controller: Nakamura, Mori Seiki, DMG MORI (GILDEMEISTER), Doosan, Biglia on Fanuc and Siemens, and the Siemens machines of the sample corpus (Monforts, STAMA, Burkhardt+Weber, Pittler, Hermle, INDEX); how each maps to the machine configuration |
| `sample-corpus.md` | The programs used to test readers: the small set kept in `../spec/examples/sources/`, and the large corpus kept outside the repository with what a survey of it showed |

How this relates to the specification: `../spec/controller-mapping.md` is organized by NCX word and says, per word, what each controller writes; the documents here are organized by controller and say how that controller thinks, including the things NCX keeps as `RAW`. When the two disagree, the mapping is the specification and this folder is the background; fix the background.

Sources, by title (none of them is in the repository; all are the official documents of the controller and machine builders and are available from them):

- Heidenhain: iTNC 530 user manual for conversational programming (Klartext); iTNC 530 cycle programming manual; TNC 640 cycle programming manual 892905-15 (with the turning cycles).
- Fanuc: Series 30i/300i/300is Model A user manuals (common to lathe system and machining center system; lathe system; machining center system); the machine builders' manuals below for the dialects.
- Siemens: SINUMERIK 840D sl NC programming manual 06/2019, V4.92 (fundamentals and job planning in one volume; externally programmable cycles in chapter 3.25; G groups in chapter 4.3); the English 10/2020 edition and the SINUMERIK ONE 10/2020 edition (same text for the words used here); 840D sl measuring cycles manual 06/2019; the older 840D fundamentals (11/2006), job planning and cycles manuals; the system variables list (PGA1); the ISO turning dialect manual (PGT); the "5-Achs-Bearbeitung" manual (CYCLE800, TRAORI, CYCLE832); the ShopMill and ShopTurn operating manuals; the High Level Language training documentation 2010.
- Machine builders: Nakamura-Tome Precision Turning Center programming manual 4810004E (editions EDG for the Super NTJX, ECL for the NTY3, EEE for the WY-250L), the Walter Meier dealer guides (NTJX M and G code list 2016, NTJX programming guide 2009, Nakamura programming manual 2019, AS-200L/WY guide), the Nakamura G411 jump programming manual; Doosan Puma 2600SY M function list; Mori Seiki programming manuals for the NL series, NT4300 and NTX1000SZM, the ESPRIT postprocessor manual for the NTX; DMG MORI Technik Programmierung V1.0 and V2.1 (cycle documentation), the GILDEMEISTER structure programming training manual (GMX/CTX TC), the CLX 350 and DMC 160 FD operating manuals; Biglia programming manuals volumes 1 to 3 and the Smart Turn manual.
