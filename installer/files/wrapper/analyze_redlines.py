#!/usr/bin/env python3
"""
analyze_redlines.py — Convert a redline PDF to PNG pages for Claude to analyze.

Called by the bim_monkey_analyze_redlines MCP tool in revit_mcp_wrapper.py.
Requires: pip install pymupdf

Usage:
  python analyze_redlines.py --folder "C:\\Users\\...\\BIM Monkey\\Redline Review"
"""
import argparse
import json
import os
import sys


def convert_pdf_to_pages(folder: str) -> dict:
    pdf_path = os.path.join(folder, "redline.pdf")
    if not os.path.exists(pdf_path):
        return {"success": False, "error": f"No redline.pdf found in: {folder}"}

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
        doc = fitz.open(pdf_path)
        pages = []
        for i, page in enumerate(doc):
            # 2× scale = 144 DPI — enough for Claude to read handwriting clearly
            mat = fitz.Matrix(2.0, 2.0)
            pix = page.get_pixmap(matrix=mat)
            png_path = os.path.join(folder, f"page-{i + 1:03d}.png")
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
    parser.add_argument("--folder", required=True, help="Path to Redline Review folder")
    args = parser.parse_args()
    result = convert_pdf_to_pages(args.folder)
    print(json.dumps(result, indent=2))
    sys.exit(0 if result.get("success") else 1)
