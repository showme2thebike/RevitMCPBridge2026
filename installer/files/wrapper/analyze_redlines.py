#!/usr/bin/env python3
"""
analyze_redlines.py — Convert a redline PDF to PNG pages for Claude to analyze.

Called by the bim_monkey_analyze_redlines MCP tool in revit_mcp_wrapper.py.
Requires: pip install pymupdf

Usage:
  python analyze_redlines.py --pdf "C:\\path\\to\\redline_20260324_143022.pdf" --folder "C:\\...\\Redline Review"
  python analyze_redlines.py --folder "C:\\...\\Redline Review"   # legacy: looks for redline.pdf in folder
"""
import argparse
import json
import os
import sys


def convert_pdf_to_pages(pdf_path: str, output_folder: str) -> dict:
    if not os.path.exists(pdf_path):
        return {"success": False, "error": f"PDF not found: {pdf_path}"}

    try:
        import fitz  # PyMuPDF
    except ImportError:
        return {
            "success": False,
            "error": (
                "PyMuPDF not installed. Run: pip install pymupdf\n"
                "Then retry the redline analysis."
            )
        }

    try:
        os.makedirs(output_folder, exist_ok=True)
        doc = fitz.open(pdf_path)
        pages = []
        # 300 DPI — readable for small annotation text and handwriting
        scale = 300 / 72
        mat = fitz.Matrix(scale, scale)
        for i, page in enumerate(doc):
            pix = page.get_pixmap(matrix=mat)
            png_path = os.path.join(output_folder, f"page-{i + 1:03d}.png")
            pix.save(png_path)
            pages.append({
                "page": i + 1,
                "path": png_path,
                "width_px": pix.width,
                "height_px": pix.height,
            })
        doc.close()

        return {
            "success": True,
            "pdfPath": pdf_path,
            "pageCount": len(pages),
            "pages": pages,
        }
    except Exception as e:
        return {"success": False, "error": str(e)}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--pdf", help="Full path to the PDF file")
    parser.add_argument("--folder", required=True, help="Output folder for PNG pages")
    args = parser.parse_args()

    # Resolve PDF path: --pdf takes priority, then look for redline.pdf in folder
    if args.pdf:
        resolved = args.pdf
    else:
        resolved = os.path.join(args.folder, "redline.pdf")

    result = convert_pdf_to_pages(resolved, args.folder)
    print(json.dumps(result, indent=2))
    sys.exit(0 if result.get("success") else 1)
