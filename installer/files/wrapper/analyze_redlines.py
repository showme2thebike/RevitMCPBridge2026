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


def check_annotations(doc) -> dict:
    """Quick pre-scan for PDF annotation objects before committing to full conversion."""
    total_annots = 0
    annot_types = set()
    for page in doc:
        for annot in page.annots():
            total_annots += 1
            annot_types.add(annot.type[1])  # e.g. "Highlight", "Ink", "FreeText", "Square"
    return {"count": total_annots, "types": sorted(annot_types)}


def convert_pdf_to_pages(pdf_path: str, output_folder: str) -> dict:
    if not os.path.exists(pdf_path):
        return {"success": False, "error": f"PDF not found: {pdf_path}"}

    try:
        import fitz  # PyMuPDF
    except ImportError:
        return {
            "success": False,
            "error": "PyMuPDF not installed. Run: pip install pymupdf\nThen retry the redline analysis."
        }

    try:
        os.makedirs(output_folder, exist_ok=True)
        doc = fitz.open(pdf_path)

        # Pre-scan for annotation objects before slow conversion
        annot_info = check_annotations(doc)
        if annot_info["count"] == 0:
            # Still convert — baked-in vector markup won't show as /Annots
            annotation_warning = (
                "No PDF annotation objects found (0 /Annots). "
                "This PDF may use baked-in vector markup (revision clouds, callout boxes drawn as geometry) "
                "rather than digital ink annotations. Markup detection requires visual image analysis. "
                "If this is an approved set with no new markup, this may be the wrong file."
            )
        else:
            annotation_warning = None

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

        result = {
            "success": True,
            "pdfPath": pdf_path,
            "pageCount": len(pages),
            "pages": pages,
            "annotationObjects": annot_info,
        }
        if annotation_warning:
            result["warning"] = annotation_warning
        return result

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
