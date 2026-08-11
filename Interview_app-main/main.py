from __future__ import annotations

import argparse
import json
from pathlib import Path

from src.cv_parser import get_cv_profile_from_csv, profile_to_json


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="CV interview RAG: direct CV JSON + Qdrant question bank + Ollama Qwen"
    )
    sub = parser.add_subparsers(dest="command", required=True)

    ingest = sub.add_parser("ingest-questions", help="Upload only the question bank to Qdrant")
    ingest.add_argument("--question-vi", type=Path)
    ingest.add_argument("--question-en", type=Path)
    ingest.add_argument("--recreate", action="store_true")

    show = sub.add_parser("show-profile", help="Normalize one candidate from CVExtract.csv")
    show.add_argument("--cv-file", type=Path, required=True)
    show.add_argument("--candidate-id", required=True)

    generate = sub.add_parser("generate", help="Generate questions from one CV without storing it in Qdrant")
    generate.add_argument("--cv-file", type=Path, required=True)
    generate.add_argument("--candidate-id", required=True)
    generate.add_argument("--type", choices=["technical", "behavioral"], default="technical")
    generate.add_argument("--count", type=int, default=3, choices=[1, 2, 3])
    generate.add_argument("--language", choices=["vi", "en"], default="vi")
    generate.add_argument("--output", type=Path)

    evaluate = sub.add_parser("evaluate", help="Evaluate one answer using the direct CV profile")
    evaluate.add_argument("--cv-file", type=Path, required=True)
    evaluate.add_argument("--candidate-id", required=True)
    evaluate.add_argument("--question-json", type=Path, required=True)
    evaluate.add_argument("--answer", required=True)

    coding = sub.add_parser("coding-score", help="Convert test-case pass rate to coding score")
    coding.add_argument("--pass-rate", type=float, required=True)

    final = sub.add_parser("final-score", help="Calculate final interview score")
    final.add_argument("--technical", type=float, required=True)
    final.add_argument("--coding", type=float, required=True)
    final.add_argument("--behavioral", type=float, required=True)
    return parser


def _read_question(path: Path) -> dict:
    question_data = json.loads(path.read_text(encoding="utf-8"))
    if isinstance(question_data, list):
        return question_data[0]
    if isinstance(question_data, dict) and isinstance(question_data.get("questions"), list):
        return question_data["questions"][0]
    if isinstance(question_data, dict):
        return question_data
    raise ValueError("question-json must contain a question object or a questions array")


def main() -> None:
    args = build_parser().parse_args()
    if args.command == "show-profile":
        profile = get_cv_profile_from_csv(args.cv_file, args.candidate_id)
        print(profile_to_json(profile))
        return

    from src.config import get_settings

    settings = get_settings()

    if args.command == "ingest-questions":
        from src.ingest import ingest_question_bank
        from src.vector_store import VectorStore

        store = VectorStore(settings)
        question_counts = ingest_question_bank(
            store,
            settings,
            args.question_vi,
            args.question_en,
            recreate=args.recreate,
        )
        print(json.dumps({"collections": question_counts, "total_question_chunks": sum(question_counts.values())}, ensure_ascii=False, indent=2))
        return

    from src.scoring import RubricScorer

    scorer = RubricScorer(settings.rubric_path)
    if args.command == "coding-score":
        print(json.dumps({"coding_score": scorer.coding_score(args.pass_rate)}, indent=2))
        return
    if args.command == "final-score":
        print(
            json.dumps(
                scorer.final_score(args.technical, args.coding, args.behavioral),
                ensure_ascii=False,
                indent=2,
            )
        )
        return

    profile = get_cv_profile_from_csv(args.cv_file, args.candidate_id)
    from src.interview_service import InterviewService

    service = InterviewService(settings)
    if args.command == "generate":
        questions = service.generate_questions(profile, args.type, args.count, args.language)
        output = {"candidate_id": profile.get("candidate_id"), "questions": questions}
        rendered = json.dumps(output, ensure_ascii=False, indent=2)
        if args.output:
            args.output.write_text(rendered, encoding="utf-8")
            print(f"Saved: {args.output}")
        else:
            print(rendered)
        return

    if args.command == "evaluate":
        result = service.evaluate_answer(profile, _read_question(args.question_json), args.answer)
        print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
