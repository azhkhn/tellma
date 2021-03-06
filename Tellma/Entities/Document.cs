﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tellma.Entities
{
    [StrongEntity]
    public class DocumentForSave<TDocumentLine, TAttachment> : EntityWithKey<int>
    {
        [Display(Name = "Document_DocumentDate")]
        [Required(ErrorMessage = nameof(RequiredAttribute))]
        public DateTime? DocumentDate { get; set; }

        // HIDDEN

        [Display(Name = "Document_VoucherBooklet")]
        public int? VoucherBookletId { get; set; } // HIDDEN

        [Display(Name = "Document_VoucherNumericReference")]
        public int? VoucherNumericReference { get; set; }

        [Display(Name = "Document_DocumentLookup1")]
        public int? DocumentLookup1Id { get; set; }

        [Display(Name = "Document_DocumentLookup2")]
        public int? DocumentLookup2Id { get; set; }

        [Display(Name = "Document_DocumentLookup3")]
        public int? DocumentLookup3Id { get; set; }

        [Display(Name = "Document_DocumentText1")]
        [StringLength(255, ErrorMessage = nameof(StringLengthAttribute))]
        public string DocumentText1 { get; set; }

        [Display(Name = "Document_DocumentText2")]
        [StringLength(255, ErrorMessage = nameof(StringLengthAttribute))]
        public string DocumentText2 { get; set; }

        // END HIDDEN

        [Display(Name = "Memo")]
        [StringLength(255, ErrorMessage = nameof(StringLengthAttribute))]
        public string Memo { get; set; }

        [Display(Name = "Document_MemoIsCommon")]
        [DefaultValue(true)]
        public bool? MemoIsCommon { get; set; }

        [ForeignKey(nameof(Line.DocumentId))]
        public List<TDocumentLine> Lines { get; set; }

        [Display(Name = "Document_Attachments")]
        [ForeignKey(nameof(Attachment.DocumentId))]
        public List<TAttachment> Attachments { get; set; }
    }

    public class DocumentForSave : DocumentForSave<LineForSave, AttachmentForSave>
    {

    }

    public class Document : DocumentForSave<Line, Attachment>
    {
        [Display(Name = "Definition")]
        public string DefinitionId { get; set; }

        [Display(Name = "Document_SerialNumber")]
        [AlwaysAccessible]
        public int? SerialNumber { get; set; }

        [Display(Name = "Document_State")]
        [AlwaysAccessible]
        [ChoiceList(new object[] {
            DocState.Draft,
            DocState.Void,
            DocState.Requested,
            DocState.Rejected,
            DocState.Authorized,
            DocState.Failed,
            DocState.Completed,
            DocState.Invalid,
            DocState.Reviewed,
            DocState.Closed
        },
            new string[] {
            DocStateName.Draft,
            DocStateName.Void,
            DocStateName.Requested,
            DocStateName.Rejected,
            DocStateName.Authorized,
            DocStateName.Failed,
            DocStateName.Completed,
            DocStateName.Invalid,
            DocStateName.Reviewed,
            DocStateName.Closed
        })]
        public short? State { get; set; }

        [Display(Name = "Document_Comment")]
        public string Comment { get; set; }

        [Display(Name = "Document_Assignee")]
        public int? AssigneeId { get; set; }

        [Display(Name = "Document_AssignedAt")]
        public DateTimeOffset? AssignedAt { get; set; }

        [Display(Name = "Document_AssignedBy")]
        public int? AssignedById { get; set; }

        [Display(Name = "Document_OpenedAt")]
        public DateTimeOffset? OpenedAt { get; set; }

        [Display(Name = "CreatedAt")]
        public DateTimeOffset? CreatedAt { get; set; }

        [Display(Name = "CreatedBy")]
        public int? CreatedById { get; set; }

        [Display(Name = "ModifiedAt")]
        public DateTimeOffset? ModifiedAt { get; set; }

        [Display(Name = "ModifiedBy")]
        public int? ModifiedById { get; set; }

        // For Query

        [Display(Name = "Document_Assignee")]
        [ForeignKey(nameof(AssigneeId))]
        public User Assignee { get; set; }

        [Display(Name = "Document_AssignedBy")]
        [ForeignKey(nameof(AssignedById))]
        public User AssignedBy { get; set; }

        [Display(Name = "CreatedBy")]
        [ForeignKey(nameof(CreatedById))]
        public User CreatedBy { get; set; }

        [Display(Name = "ModifiedBy")]
        [ForeignKey(nameof(ModifiedById))]
        public User ModifiedBy { get; set; }

        [Display(Name = "Document_Signatures")]
        [ForeignKey(nameof(DocumentSignature.DocumentId))]
        public List<DocumentSignature> Signatures { get; set; }

        [Display(Name = "Document_AssignmentsHistory")]
        [ForeignKey(nameof(DocumentAssignment.DocumentId))]
        public List<DocumentAssignment> AssignmentsHistory { get; set; }

        // HIDDEN

        [Display(Name = "Document_VoucherBooklet")]
        [ForeignKey(nameof(VoucherBookletId))]
        public VoucherBooklet VoucherBooklet { get; set; }

        [Display(Name = "Document_DocumentLookup1")]
        [ForeignKey(nameof(DocumentLookup1Id))]
        public Lookup DocumentLookup1 { get; set; }

        [Display(Name = "Document_DocumentLookup2")]
        [ForeignKey(nameof(DocumentLookup2Id))]
        public Lookup DocumentLookup2 { get; set; }

        [Display(Name = "Document_DocumentLookup3")]
        [ForeignKey(nameof(DocumentLookup3Id))]
        public Lookup DocumentLookup3 { get; set; }

        // END HIDDEN
    }

    public static class DocState
    {
        public const short Draft = 0;
        public const short Void = -1;
        public const short Requested = 1;
        public const short Rejected = -2;
        public const short Authorized = 2;
        public const short Failed = -3;
        public const short Completed = 3;
        public const short Invalid = -4;
        public const short Reviewed = 4;
        public const short Closed = 5;
    }

    public static class DocStateName
    {
        private const string _prefix = "Document_State_";

        public const string Draft = _prefix + nameof(Draft);
        public const string Void = _prefix + nameof(Void);
        public const string Requested = _prefix + nameof(Requested);
        public const string Rejected = _prefix + nameof(Rejected);
        public const string Authorized = _prefix + nameof(Authorized);
        public const string Failed = _prefix + nameof(Failed);
        public const string Completed = _prefix + nameof(Completed);
        public const string Invalid = _prefix + nameof(Invalid);
        public const string Reviewed = _prefix + nameof(Reviewed);
        public const string Closed = _prefix + nameof(Closed);
    }
}
